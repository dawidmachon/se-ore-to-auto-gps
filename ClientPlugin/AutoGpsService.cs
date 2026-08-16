using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace ClientPlugin;

// Legit auto-marker: it ONLY marks ore the player's ore detector has actually detected.
// A background voxel read measures the size/extent of those already-detected deposits
// (and groups nearby small ones) - any ore the detector has not found is discarded and never
// marked.
//
// Player-interaction gate (PluginHub requirement): markers are created ONLY when the player
// presses the configured key (settings -> "Mark detected ore"). Detection and background
// sizing accumulate silently, and one keypress publishes everything the detector has found so
// far - so an AFK script that never sends input never gets a single marker.
//
// Sizing scans are centred on each detected deposit's OWN position (not the ship), so it works
// correctly at any speed. Newly detected positions queue up and are sized one area per cycle.
public static class AutoGpsService
{
    private struct Component
    {
        public Vector3D Position;
        public int SolidVoxels;
        public double SpatialRadius;
        public List<Vector3D> Members;
        public double OreRatio;
        public double IngotRatio;
    }

    private class PublishedEntry
    {
        public int Hash;
        public Vector3D Position;
        public int SolidVoxels;
        public double SpatialRadius;
        public int Count;
        public bool IsField;
        public List<Vector3D> Members;
    }

    // Legit gate: ore the detector detected (material -> detected positions), light de-duped.
    private static readonly ConcurrentQueue<FoundOre> s_capture = new ConcurrentQueue<FoundOre>();
    private static readonly Dictionary<string, List<Vector3D>> s_detected = new Dictionary<string, List<Vector3D>>();

    // Detected positions waiting to be sized. Main-thread only (fed from the capture drain).
    private static readonly Queue<Vector3D> s_pendingSizing = new Queue<Vector3D>();

    private static readonly ConcurrentQueue<List<FoundOre>> s_scanResults = new ConcurrentQueue<List<FoundOre>>();
    private static volatile bool s_scanRunning;
    private static readonly Dictionary<string, List<FoundOre>> s_pending = new Dictionary<string, List<FoundOre>>();
    private static readonly Dictionary<string, List<PublishedEntry>> s_published = new Dictionary<string, List<PublishedEntry>>();
    private static readonly Dictionary<int, PublishedEntry> s_publishedByHash = new Dictionary<int, PublishedEntry>();

    // Sizing cadence and per-deposit scan radius - tuned, not user-tunable.
    private const double SizingIntervalSeconds = 0.4;
    private const double SizingRadius = 350.0;
    private static double s_nextSizingSeconds;

    // One-time reminder that no mark key is bound (the gate cannot work without it).
    private static bool s_warnedNoKey;

    private static readonly Dictionary<string, Color> s_colors = new Dictionary<string, Color>
    {
        { "Iron",      new Color(190, 190, 190) },
        { "Nickel",    new Color(120, 150, 145) },
        { "Cobalt",    new Color( 70, 110, 220) },
        { "Magnesium", new Color(220, 195,  90) },
        { "Silicon",   new Color(170, 140, 190) },
        { "Silver",    new Color(230, 230, 240) },
        { "Gold",      new Color(255, 205,  45) },
        { "Platinum",  new Color(215, 235, 245) },
        { "Uranium",   new Color( 95, 230,  95) },
        { "Ice",       new Color(155, 215, 255) },
        { "Stone",     new Color(150, 130, 110) },
    };

    public static void HandleUpdate()
    {
        var session = MySession.Static;
        if (session == null) return;
        double now = session.ElapsedPlayTime.TotalSeconds;

        while (s_capture.TryDequeue(out var o))
            AddDetected(o.Material, o.Position);

        // A sizing scan just completed -> accumulate. Publishing waits for the player's keypress
        // (player-interaction gate); until then nothing touches the GPS list.
        if (s_scanResults.TryDequeue(out var results))
        {
            s_scanRunning = false;
            foreach (var o in results) AddPending(o);
        }

        // Player-interaction gate: the only place GPS markers get created.
        HandleMarkInput();

        // Size newly detected ore: scan around the deposit's own position (works at any speed).
        // One area per cycle, throttled, so a stream of detections is processed performantly.
        if (s_pendingSizing.Count > 0 && !s_scanRunning && now >= s_nextSizingSeconds)
        {
            Vector3D center = s_pendingSizing.Dequeue();
            s_scanRunning = true;
            VoxelScan.Run(center, SizingRadius, list => s_scanResults.Enqueue(list));
            s_nextSizingSeconds = now + SizingIntervalSeconds;
        }
    }

    // Legit input, from the Harmony postfix (any thread).
    public static void CaptureDeposit(MyEntityOreDeposit deposit)
    {
        if (deposit == null) return;
        var voxelMap = deposit.VoxelMap;
        if (voxelMap == null) return;

        Vector3D refPos;
        try { refPos = voxelMap.PositionComp.GetPosition() - (Vector3D)voxelMap.StorageMin; }
        catch { return; }

        foreach (var data in deposit.Materials)
        {
            var def = data.Material;
            if (def == null) continue;
            string material = !string.IsNullOrEmpty(def.MinedOre) ? def.MinedOre : def.Id.SubtypeName;
            if (string.IsNullOrEmpty(material)) continue;

            Vector3 local = data.AverageLocalPosition;
            Vector3D world;
            try { MyVoxelCoordSystems.LocalPositionToWorldPosition(refPos, ref local, out world); }
            catch { continue; }

            s_capture.Enqueue(new FoundOre { Material = material, Position = world, SolidVoxels = 0, SpatialRadius = 0 });
        }
    }

    // Player-interaction gate (PluginHub requirement). Publishes accumulated, detector-verified
    // deposits ONLY on the configured keypress, so ore can never be marked while AFK.
    private static void HandleMarkInput()
    {
        var key = Config.Current.MarkKey;
        if (key.Key == MyKeys.None)
        {
            // Without a key nothing can ever be marked - point the player at the setting once.
            if (!s_warnedNoKey && s_detected.Count > 0)
            {
                s_warnedNoKey = true;
                Notify("Ore to Auto Gps: ore detected - bind the 'Mark detected ore' key in the plugin settings to mark it.");
            }
            return;
        }

        var input = MyInput.Static;
        if (input == null) return;
        if (MyScreenManager.FocusedControl != null) return; // typing in chat / a text field

        if (key.HasPressed(input))
        {
            Publish(out int added, out int updated, out int skipped);
            if (added > 0 || updated > 0)
                Notify("Ore to Auto Gps: " + added + " new, " + updated + " updated marker(s).");
            else
                Notify("Ore to Auto Gps: no detected ore waiting to be marked.");
        }
    }

    private static void Notify(string message)
    {
        try { MyVisualScriptLogicProvider.ShowNotification(message, 5000); }
        catch { }
    }

    // Records a detection (legit gate) and queues it for sizing if it is new.
    private static void AddDetected(string material, Vector3D pos)
    {
        const double dupSq = 100.0 * 100.0;

        if (!s_detected.TryGetValue(material, out var list))
        { list = new List<Vector3D>(); s_detected[material] = list; }
        foreach (var p in list)
            if (Vector3D.DistanceSquared(p, pos) <= dupSq) return; // already known
        list.Add(pos);

        foreach (var q in s_pendingSizing)
            if (Vector3D.DistanceSquared(q, pos) <= dupSq) return; // already queued for sizing
        s_pendingSizing.Enqueue(pos);
    }

    private static void AddPending(FoundOre o)
    {
        if (!s_pending.TryGetValue(o.Material, out var list))
        { list = new List<FoundOre>(); s_pending[o.Material] = list; }
        list.Add(o);
    }

    private static void Publish(out int added, out int updated, out int skipped)
    {
        added = 0; updated = 0; skipped = 0;

        var session = MySession.Static;
        if (session == null || session.LocalPlayerId == 0) { s_pending.Clear(); return; }

        long identityId;
        IMyGpsCollection gps;
        try { identityId = session.LocalPlayerId; gps = ((IMySession)session).GPS; }
        catch { s_pending.Clear(); return; }
        if (gps == null) { s_pending.Clear(); return; }

        var cfg = Config.Current;
        int dedup = Math.Max(1, cfg.DedupRadiusMeters);
        long minorM3 = Math.Max(0, cfg.MinorThreshold);
        int fieldRadius = Math.Max(1, cfg.FieldRadius);

        foreach (var kv in s_pending)
        {
            string material = kv.Key;
            var points = kv.Value;
            if (!IsOreEnabled(material, cfg)) { skipped += points.Count; continue; }

            var deposits = ClusterComponents(points, dedup);

            var notable = new List<Component>();
            var minor = new List<Component>();
            foreach (var d in deposits)
            {
                // Legit gate: only keep deposits the detector actually found.
                if (!IsDetected(material, d.Position, d.SpatialRadius, dedup)) { skipped++; continue; }
                long m3 = (long)d.SolidVoxels * 64;
                if (minorM3 <= 0 || m3 >= minorM3) notable.Add(d); else minor.Add(d);
            }

            if (notable.Count == 0 && minor.Count == 0) continue;

            if (!s_published.TryGetValue(material, out var published))
            { published = new List<PublishedEntry>(); s_published[material] = published; }

            foreach (var d in notable)
                HandleComponent(gps, identityId, material, d, false, published, cfg, ref added, ref updated, ref skipped);

            if (minor.Count > 0 && minorM3 > 0)
            {
                var minorPoints = new List<FoundOre>(minor.Count);
                foreach (var m in minor)
                    minorPoints.Add(new FoundOre { Material = material, Position = m.Position, SolidVoxels = m.SolidVoxels, SpatialRadius = m.SpatialRadius });
                foreach (var f in ClusterComponents(minorPoints, fieldRadius))
                {
                    bool anyDetected = f.Members != null && f.Members.Count > 0 &&
                        f.Members.Exists(mp => IsDetected(material, mp, 0, dedup));
                    if (!anyDetected) { skipped++; continue; }
                    int count = f.Members != null ? f.Members.Count : 1;
                    HandleComponent(gps, identityId, material, f, true, published, cfg, ref added, ref updated, ref skipped, fieldCount: count);
                }
            }
        }

        s_pending.Clear();
    }

    // True if the detector reported this material near the given position.
    private static bool IsDetected(string material, Vector3D pos, double spatialRadius, int dedup)
    {
        if (!s_detected.TryGetValue(material, out var list) || list.Count == 0) return false;
        double reach = spatialRadius + dedup;
        double reachSq = reach * reach;
        foreach (var p in list)
            if (Vector3D.DistanceSquared(p, pos) <= reachSq) return true;
        return false;
    }

    private static void HandleComponent(IMyGpsCollection gps, long identityId, string material, Component comp, bool isField, List<PublishedEntry> published, Config cfg, ref int added, ref int updated, ref int skipped, int fieldCount = 1)
    {
        int dedup = Math.Max(1, cfg.DedupRadiusMeters);
        int fieldRadius = Math.Max(1, cfg.FieldRadius);

        PublishedEntry match = null;
        foreach (var entry in published)
        {
            if (entry.IsField != isField) continue; // fields accumulate with fields; notables with notables
            double reach = (isField ? fieldRadius : dedup) + entry.SpatialRadius;
            if (Vector3D.DistanceSquared(entry.Position, comp.Position) <= reach * reach) { match = entry; break; }
        }

        if (match == null)
        {
            if (CreateGps(gps, identityId, material, comp, isField, fieldCount, cfg, out var entry))
            { published.Add(entry); s_publishedByHash[entry.Hash] = entry; added++; }
            else skipped++;
        }
        else if (isField)
        {
            if (MergeField(gps, identityId, material, match, comp, dedup, cfg)) updated++;
            else skipped++;
        }
        else if (comp.SolidVoxels > match.SolidVoxels)
        {
            if (UpgradeGps(gps, identityId, material, match, comp, isField, fieldCount, cfg)) updated++;
            else skipped++;
        }
        else skipped++;
    }

    // Accumulates a newly scanned group of small deposits into an existing field marker.
    // Only genuinely new positions are added, so re-scanning the same traces never inflates the count.
    private static bool MergeField(IMyGpsCollection gps, long identityId, string material, PublishedEntry entry, Component comp, int dedup, Config cfg)
    {
        double dedupSq = (double)dedup * dedup;
        var fresh = new List<Vector3D>();
        if (comp.Members != null)
        {
            foreach (var m in comp.Members)
            {
                bool dup = false;
                if (entry.Members != null)
                {
                    foreach (var em in entry.Members)
                    {
                        if (Vector3D.DistanceSquared(m, em) <= dedupSq) { dup = true; break; }
                    }
                }
                if (!dup) fresh.Add(m);
            }
        }
        if (fresh.Count == 0) return false; // already fully covered (re-scan)

        int compCount = (comp.Members != null && comp.Members.Count > 0) ? comp.Members.Count : 1;
        double perMember = (double)comp.SolidVoxels / compCount;
        int addSolid = (int)Math.Round(perMember * fresh.Count);

        Vector3D freshCenter = Vector3D.Zero;
        foreach (var m in fresh) freshCenter += m;
        freshCenter /= fresh.Count;

        double total = entry.SolidVoxels + (double)addSolid;
        Vector3D newCenter = total > 0 ? (entry.Position * entry.SolidVoxels + freshCenter * addSolid) / total : entry.Position;

        if (entry.Members == null) entry.Members = new List<Vector3D>();
        entry.Members.AddRange(fresh);
        if (entry.Members.Count > 64) entry.Members.RemoveRange(0, entry.Members.Count - 64);

        double maxD = 0;
        foreach (var m in entry.Members) { double d = Vector3D.DistanceSquared(newCenter, m); if (d > maxD) maxD = d; }

        int newCount = entry.Count + fresh.Count;
        int newSolid = (int)total;
        double newRadius = Math.Sqrt(maxD);

        var merged = new Component { Position = newCenter, SolidVoxels = newSolid, SpatialRadius = newRadius, Members = entry.Members };
        BuildGpsText(material, merged, true, newCount, cfg, out string name, out string desc);
        Color color = s_colors.TryGetValue(material, out var c) ? c : Color.Yellow;
        try
        {
            gps.RemoveGps(identityId, entry.Hash);
            s_publishedByHash.Remove(entry.Hash);
            var g = gps.Create(name, desc, newCenter, cfg.ShowOnHud, false);
            if (g == null) return false;
            g.GPSColor = color;
            gps.AddGps(identityId, g);
            entry.Hash = g.Hash;
            entry.Position = newCenter;
            entry.SolidVoxels = newSolid;
            entry.SpatialRadius = newRadius;
            entry.Count = newCount;
            entry.IsField = true;
            s_publishedByHash[g.Hash] = entry;
            return true;
        }
        catch { return false; }
    }

    private static List<Component> ClusterComponents(List<FoundOre> points, double linkRadius)
    {
        var result = new List<Component>();
        int n = points.Count;
        if (n == 0) return result;
        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;

        if (n > 1)
        {
            var grid = new Dictionary<(int, int, int), List<int>>();
            double inv = 1.0 / linkRadius;
            for (int i = 0; i < n; i++)
            {
                Vector3D s = points[i].Position * inv;
                var key = ((int)Math.Floor(s.X), (int)Math.Floor(s.Y), (int)Math.Floor(s.Z));
                if (!grid.TryGetValue(key, out var bucket)) { bucket = new List<int>(); grid[key] = bucket; }
                bucket.Add(i);
            }
            double r2 = linkRadius * linkRadius;
            foreach (var kv in grid)
            {
                var k = kv.Key; var bucket = kv.Value;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!grid.TryGetValue((k.Item1 + dx, k.Item2 + dy, k.Item3 + dz), out var other)) continue;
                    foreach (int i in bucket)
                    foreach (int j in other)
                    {
                        if (j <= i) continue;
                        if (Vector3D.DistanceSquared(points[i].Position, points[j].Position) <= r2) Union(parent, i, j);
                    }
                }
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(parent, i);
            if (!groups.TryGetValue(root, out var list)) { list = new List<int>(); groups[root] = list; }
            list.Add(i);
        }
        foreach (var g in groups.Values)
        {
            double totalW = 0; Vector3D center = Vector3D.Zero; int totalSolid = 0;
            var members = new List<Vector3D>(g.Count);
            foreach (int idx in g)
            {
                int w = points[idx].SolidVoxels > 0 ? points[idx].SolidVoxels : 1;
                center += points[idx].Position * w; totalW += w; totalSolid += points[idx].SolidVoxels;
                members.Add(points[idx].Position);
            }
            if (totalW > 0) center /= totalW;
            double maxDistSq = 0;
            foreach (int idx in g) { double d = Vector3D.DistanceSquared(center, points[idx].Position); if (d > maxDistSq) maxDistSq = d; }
            result.Add(new Component { Position = center, SolidVoxels = totalSolid, SpatialRadius = Math.Sqrt(maxDistSq), Members = members, OreRatio = points.Count > 0 ? points[0].OreRatio : 0, IngotRatio = points.Count > 0 ? points[0].IngotRatio : 0 });
        }
        return result;
    }

    private static int Find(int[] parent, int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
    private static void Union(int[] parent, int a, int b) { int ra = Find(parent, a), rb = Find(parent, b); if (ra != rb) parent[ra] = rb; }

    private static bool CreateGps(IMyGpsCollection gps, long identityId, string material, Component comp, bool isField, int fieldCount, Config cfg, out PublishedEntry entry)
    {
        entry = null;
        try
        {
            BuildGpsText(material, comp, isField, fieldCount, cfg, out string name, out string desc);
            Color color = s_colors.TryGetValue(material, out var c) ? c : Color.Yellow;
            var g = gps.Create(name, desc, comp.Position, cfg.ShowOnHud, false);
            if (g == null) return false;
            g.GPSColor = color; gps.AddGps(identityId, g);
            entry = new PublishedEntry { Hash = g.Hash, Position = comp.Position, SolidVoxels = comp.SolidVoxels, SpatialRadius = comp.SpatialRadius, Count = isField ? fieldCount : 1, IsField = isField, Members = isField ? comp.Members : null };
            return true;
        }
        catch { return false; }
    }

    private static bool UpgradeGps(IMyGpsCollection gps, long identityId, string material, PublishedEntry entry, Component comp, bool isField, int fieldCount, Config cfg)
    {
        try
        {
            BuildGpsText(material, comp, isField, fieldCount, cfg, out string name, out string desc);
            Color color = s_colors.TryGetValue(material, out var c) ? c : Color.Yellow;
            gps.RemoveGps(identityId, entry.Hash); s_publishedByHash.Remove(entry.Hash);
            var g = gps.Create(name, desc, comp.Position, cfg.ShowOnHud, false);
            if (g == null) return false;
            g.GPSColor = color; gps.AddGps(identityId, g);
            entry.Hash = g.Hash; entry.Position = comp.Position; entry.SolidVoxels = comp.SolidVoxels; entry.SpatialRadius = comp.SpatialRadius;
            entry.Count = isField ? fieldCount : 1; entry.IsField = isField; entry.Members = isField ? comp.Members : null;
            s_publishedByHash[g.Hash] = entry;
            return true;
        }
        catch { return false; }
    }

    private static void BuildGpsText(string material, Component comp, bool isField, int fieldCount, Config cfg, out string name, out string desc)
    {
        long approxM3 = (long)comp.SolidVoxels * 64;
        long oreKg = comp.OreRatio > 0 ? (long)(approxM3 * comp.OreRatio) : 0;
        long ingotKg = comp.IngotRatio > 0 ? (long)(approxM3 * comp.IngotRatio) : 0;
        // Description kept short (vanilla multiline-text crash). Show kg only - what the inventory shows.
        string yield = oreKg > 0
            ? Compact(oreKg) + " kg ore" + (ingotKg > 0 ? " -> " + Compact(ingotKg) + " kg ingots @100%" : "")
            : "";
        string coord = comp.Position.X.ToString("F0", CultureInfo.InvariantCulture) + "," + comp.Position.Y.ToString("F0", CultureInfo.InvariantCulture) + "," + comp.Position.Z.ToString("F0", CultureInfo.InvariantCulture);

        if (isField && fieldCount > 1)
        {
            name = material + " x" + fieldCount.ToString(CultureInfo.InvariantCulture);
            if (cfg.IncludeCoordsInName) name += " " + coord;
            var d = new StringBuilder();
            d.Append(fieldCount).Append(" deposits");
            if (yield.Length > 0) d.Append(", ").Append(yield);
            desc = d.ToString();
            return;
        }

        name = material;
        if (cfg.ShowQuantity && comp.SolidVoxels > 0) name += " ~" + SizeWord(oreKg);
        if (cfg.IncludeCoordsInName) name += " " + coord;
        var sb = new StringBuilder();
        if (comp.SolidVoxels > 0)
            sb.Append(yield.Length > 0 ? yield : "sized");
        else
            sb.Append("detected, sizing...");
        desc = sb.ToString();
    }

    // SE1-style compact number: 999 / 12.3k / 1.2M / 3.4B.
    private static string Compact(long n)
    {
        if (n < 1000L) return n.ToString(CultureInfo.InvariantCulture);
        double v; string suf;
        if (n < 1000000L) { v = n / 1e3; suf = "k"; }
        else if (n < 1000000000L) { v = n / 1e6; suf = "M"; }
        else { v = n / 1e9; suf = "B"; }
        return v.ToString("F1", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.') + suf;
    }

    // Size tier by ore kg (the mined yield - what the inventory shows). Thresholds tunable.
    private static string SizeWord(long oreKg)
    {
        if (oreKg < 10000L) return "Trace";
        if (oreKg < 100000L) return "Small";
        if (oreKg < 1000000L) return "Medium";
        if (oreKg < 5000000L) return "Large";
        return "Huge";
    }

    public static int ClearAll()
    {
        var session = MySession.Static;
        IMyGpsCollection gps = null; long identityId = 0;
        try { if (session != null && session.LocalPlayerId != 0) { identityId = session.LocalPlayerId; gps = ((IMySession)session).GPS; } } catch { }
        int removed = 0;
        if (gps != null && identityId != 0)
            foreach (var e in s_publishedByHash.Values) { try { gps.RemoveGps(identityId, e.Hash); removed++; } catch { } }
        s_published.Clear(); s_publishedByHash.Clear(); s_pending.Clear();
        return removed;
    }

    public static void Reset()
    {
        while (s_capture.TryDequeue(out _)) { }
        while (s_scanResults.TryDequeue(out _)) { }
        s_detected.Clear(); s_pendingSizing.Clear(); s_pending.Clear(); s_published.Clear(); s_publishedByHash.Clear();
        s_scanRunning = false; s_nextSizingSeconds = 0; s_warnedNoKey = false;
    }

    public static void Log(string msg) { try { MyLog.Default.WriteLine("[OreToAutoGps] " + msg); } catch { } }

    private static bool IsOreEnabled(string material, Config cfg)
    {
        switch (material)
        {
            case "Iron": return cfg.Iron;
            case "Nickel": return cfg.Nickel;
            case "Cobalt": return cfg.Cobalt;
            case "Magnesium": return cfg.Magnesium;
            case "Silicon": return cfg.Silicon;
            case "Silver": return cfg.Silver;
            case "Gold": return cfg.Gold;
            case "Platinum": return cfg.Platinum;
            case "Uranium": return cfg.Uranium;
            case "Ice": return cfg.Ice;
            case "Stone": return cfg.Stone;
            default: return cfg.TrackModdedOres; // modded / unknown ore types (custom server ores)
        }
    }

}
