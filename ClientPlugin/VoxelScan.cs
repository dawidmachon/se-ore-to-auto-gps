using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Voxels;
using VRage.Voxels;
using VRageMath;

namespace ClientPlugin;

public struct FoundOre
{
    public string Material;
    public Vector3D Position;
    public int SolidVoxels; // LOD-2 solid sample count; x64 ~= m^3
    public double SpatialRadius; // actual extent from centroid; set by clustering
    public long Seq;             // detection order, stamped by AutoGpsService.AddPending (marker recency); 0 until then
    public double OreRatio;   // BASELINE ore kg per deposit m^3 - variant-agnostic (0.009 x server harvest x baseline MinedOreRatio x ore density); 0 if unknown
    public double IngotRatio; // BASELINE ingot kg per deposit m^3 (ore kg x blueprint mass ratio); 0 if not refinable
}

// Mode 2 engine: a first-party voxel scan that replicates the ore detector's LOD-2
// read so we capture BOTH positions and an approximate quantity per cell. Runs on a
// background thread; storage reads are pinned, exactly like the game's MyDepositQuery.
// All entity state is snapshotted on the calling (main) thread.
public static class VoxelScan
{
    private const int Lod = 2;
    private const int CellShift = 5; // 32 LOD-0 voxels per cell axis (CELL_SIZE_IN_METERS)

    // Per-ORE baseline yield (session-stable; populated lazily - scans run one at a time).
    // Keyed by ore NAME, never by voxel material byte: the ore detector only ever reports the
    // ore ("Ice", "Stone"), not the voxel variant (Ice_01 vs Snow, Stone_01 vs TritonStone...),
    // so the yield figures must not depend on the variant the scan found (PluginHub:
    // limit info to what vanilla exposes).
    private static readonly Dictionary<string, double> s_yieldOre = new Dictionary<string, double>();
    private static readonly Dictionary<string, double> s_yieldIngot = new Dictionary<string, double>();

    public static void Run(Vector3D center, double radius, Action<List<FoundOre>> onComplete)
    {
        var sphere = new BoundingSphereD(center, radius);
        var maps = new List<MyVoxelBase>();
        MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, maps);

        var jobs = new List<MapSnapshot>();
        var seen = new HashSet<MyVoxelBase>();
        foreach (var vb in maps)
        {
            var top = vb.GetTopMostParent() as MyVoxelBase;
            if (top == null) continue;
            // MyVoxelPhysics (the collision shape) is internal, so filter by type name.
            if (top.GetType().Name == "MyVoxelPhysics") continue;
            if (!seen.Add(top)) continue;
            var storage = top.Storage;
            if (storage == null) continue;

            jobs.Add(new MapSnapshot
            {
                Storage = storage,
                Size = storage.Size,
                RefCorner = top.PositionLeftBottomCorner,
                StorageMin = top.StorageMin,
                WorldMatrix = top.PositionComp.WorldMatrixRef,
                SizeInMetresHalf = top.SizeInMetresHalf,
                LocalRef = top.PositionComp.GetPosition() - (Vector3D)top.StorageMin,
            });
        }
        maps.Clear();

        Task.Run(() =>
        {
            var result = new List<FoundOre>();
            foreach (var job in jobs)
            {
                try { ScanMap(job, center, radius, result); }
                catch { /* skip a bad voxel map, keep the rest of the scan */ }
            }
            onComplete(result);
        });
    }

    private struct MapSnapshot
    {
        public IMyStorage Storage;
        public Vector3I Size;
        public Vector3D RefCorner;
        public Vector3I StorageMin;
        public MatrixD WorldMatrix;
        public Vector3 SizeInMetresHalf;
        public Vector3D LocalRef;
    }

    private static void ScanMap(MapSnapshot job, Vector3D center, double radius, List<FoundOre> result)
    {
        var storage = job.Storage;
        using var pin = storage.Pin();
        if (!pin.Valid) return;

        // World sphere -> storage voxel coords -> 32m cell coords (>>5), clamped to storage.
        Vector3D wMin = center - radius;
        Vector3D wMax = center + radius;
        MyVoxelCoordSystems.WorldPositionToVoxelCoord(job.RefCorner, ref wMin, out Vector3I vMin);
        MyVoxelCoordSystems.WorldPositionToVoxelCoord(job.RefCorner, ref wMax, out Vector3I vMax);
        vMin += job.StorageMin;
        vMax += job.StorageMin;
        ClampCoord(ref vMin, job.Size);
        ClampCoord(ref vMax, job.Size);
        Vector3I cMin = vMin >> CellShift;
        Vector3I cMax = vMax >> CellShift;

        var cache = new MyStorageData();
        cache.Resize(new Vector3I(8));

        var sum = new Vector3[256];
        var count = new int[256];
        double radiusSq = radius * radius;

        for (int cz = cMin.Z; cz <= cMax.Z; cz++)
        for (int cy = cMin.Y; cy <= cMax.Y; cy++)
        for (int cx = cMin.X; cx <= cMax.X; cx++)
        {
            Vector3I lodMin = new Vector3I(cx, cy, cz) << 3;
            Vector3I lodMax = lodMin + 7;

            storage.ReadRange(cache, MyStorageDataTypeFlags.Content, Lod, lodMin, lodMax);
            if (!cache.ContainsVoxelsAboveIsoLevel())
                continue;

            var flags = MyVoxelRequestFlags.PreciseOrePositions;
            storage.ReadRange(cache, MyStorageDataTypeFlags.Material, Lod, lodMin, lodMax, ref flags);

            Vector3I p;
            for (p.Z = 0; p.Z < 8; p.Z++)
            for (p.Y = 0; p.Y < 8; p.Y++)
            for (p.X = 0; p.X < 8; p.X++)
            {
                int idx = cache.ComputeLinear(ref p);
                if (cache.Content(idx) <= 127) continue;
                byte mat = cache.Material(idx);
                sum[mat] += (p + lodMin) * 4f + 2f;
                count[mat]++;
            }

            for (int m = 0; m < 256; m++)
            {
                int c = count[m];
                if (c == 0) continue;
                var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition((byte)m);
                if (def == null || !def.IsRare) continue;

                string material = !string.IsNullOrEmpty(def.MinedOre) ? def.MinedOre : def.Id.SubtypeName;
                if (string.IsNullOrEmpty(material)) continue;

                if (!s_yieldOre.ContainsKey(material)) { BaselineYield(material, out var oRatio, out var iRatio); s_yieldOre[material] = oRatio; s_yieldIngot[material] = iRatio; }

                // Same world-position pipeline as MyDepositQuery -> MyEntityOreDeposit.
                Vector3 avg = sum[m] / c;
                Vector3D localOffset = (Vector3D)avg - (Vector3D)job.SizeInMetresHalf;
                Vector3 localRotated = (Vector3)Vector3D.TransformNormal(localOffset, job.WorldMatrix);
                MyVoxelCoordSystems.LocalPositionToWorldPosition(job.LocalRef, ref localRotated, out Vector3D world);

                // Cell bounds are a box; keep only points inside the requested sphere.
                if (Vector3D.DistanceSquared(center, world) > radiusSq) continue;

                result.Add(new FoundOre { Material = material, Position = world, SolidVoxels = c, OreRatio = s_yieldOre[material], IngotRatio = s_yieldIngot[material] });
            }

            Array.Clear(sum, 0, sum.Length);
            Array.Clear(count, 0, count.Length);
        }
    }

    // BASELINE (variant-agnostic) yield for an ore, used for every voxel of that ore no matter
    // which variant it is. The baseline variant is the ore's standard "<Ore>_01" material when
    // it exists (Iron_01, Ice_01, ...), else the material named after the ore itself (planetary
    // "Ice"/"Stone"), else the richest variant (modded ores). Never the actual scanned variant:
    // the detector cannot distinguish variants, so neither may the figures.
    private static void BaselineYield(string oreName, out double oreRatio, out double ingotRatio)
    {
        oreRatio = 0; ingotRatio = 0;
        MyVoxelMaterialDefinition baseline = null;
        try
        {
            string preferred = oreName + "_01";
            MyVoxelMaterialDefinition named = null, richest = null;
            double richestRatio = 0;
            foreach (var def in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                if (def == null || def.MinedOre != oreName) continue;
                string sub = def.Id.SubtypeName;
                if (sub == preferred) { named = def; break; }   // rule 1: <Ore>_01
                if (named == null && sub == oreName) named = def; // rule 2: named after the ore
                if (def.MinedOreRatio > richestRatio) { richestRatio = def.MinedOreRatio; richest = def; } // rule 3
            }
            baseline = named ?? richest;
        }
        catch { }
        ComputeYield(baseline, out oreRatio, out ingotRatio);
    }

    // ore kg and ingot kg per deposit m^3 (1 voxel = 1 m^3), from the game's own definitions.
    //   oreKg   = voxels x 0.009 x serverHarvest x MinedOreRatio x oreDensity(kg/m^3)
    //   ingotKg = oreKg x blueprint(Result/Prerequisite)   <- a MASS ratio
    // See Docs/YIELD_FORMULA.md for the full derivation and game-file references.
    private static void ComputeYield(MyVoxelMaterialDefinition def, out double oreRatio, out double ingotRatio)
    {
        oreRatio = 0;     // ore kg per deposit m^3
        ingotRatio = 0;   // ingot kg per deposit m^3 (0 if not refinable)
        if (def == null) return;
        double minedRatio;
        try { minedRatio = def.MinedOreRatio > 0 ? def.MinedOreRatio : 0; } catch { return; }
        if (minedRatio <= 0) return;
        string oreName = def.MinedOre;
        if (string.IsNullOrEmpty(oreName)) return;

        // ore volume (m^3) per voxel = base harvest (0.009) x server multiplier x MinedOreRatio.
        float serverMult = 1f;
        try { serverMult = MySession.Static.Settings.HarvestRatioMultiplier; } catch { }
        double oreM3PerVoxel = 0.009 * serverMult * minedRatio;

        // ore kg per voxel = ore m^3 x ore item density (Mass kg / Volume m^3 at runtime).
        try
        {
            var oreDef = MyDefinitionManager.Static.GetPhysicalItemDefinition(MyDefinitionId.Parse("Ore/" + oreName));
            double oreDensity = (oreDef != null && oreDef.Volume > 0) ? (oreDef.Mass / oreDef.Volume) : 0;
            oreRatio = oreM3PerVoxel * oreDensity;
        }
        catch { }

        // ingot kg per voxel = ore kg x the blueprint MASS ratio (Result/Prerequisite), if refinable.
        try
        {
            foreach (var bp in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                if (bp == null || bp.Prerequisites == null || bp.Prerequisites.Length == 0) continue;
                if (bp.InputItemType != typeof(MyObjectBuilder_Ore)) continue;
                if (bp.Prerequisites[0].Id.SubtypeName != oreName) continue;
                if (bp.Results == null || bp.Results.Length == 0) continue;
                double pre = (double)bp.Prerequisites[0].Amount;
                double res = (double)bp.Results[0].Amount;
                if (pre > 0) ingotRatio = oreRatio * (res / pre);
                break;
            }
        }
        catch { }
    }

    private static void ClampCoord(ref Vector3I c, Vector3I size)
    {
        if (c.X < 0) c.X = 0; else if (c.X >= size.X) c.X = size.X - 1;
        if (c.Y < 0) c.Y = 0; else if (c.Y >= size.Y) c.Y = size.Y - 1;
        if (c.Z < 0) c.Z = 0; else if (c.Z >= size.Z) c.Z = size.Z - 1;
    }
}
