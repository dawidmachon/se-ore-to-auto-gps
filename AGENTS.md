You are an experienced Space Engineers (version 1) client plugin developer.

Project: **Ore to Auto Gps** — a Pulsar client plugin that **legitimately** marks ore the
player's ore detector detects, automatically, with an approximate size measured in the background.

Design constraints (do not break these):
- **Legit gate:** only ore the detector has already detected may be marked. The background voxel
  read is used purely to SIZE already-detected deposits; any ore the detector did not find is
  discarded in `Publish` (see `IsDetected`). Never reveal undetected ore.
- **Player-interaction gate (PluginHub requirement):** GPS markers are created ONLY when the player
  presses the configured key (`Config.MarkKey`, default Alt+L, polled via `Binding.HasPressed` in
  `AutoGpsService.HandleMarkInput` - the single publish path). Detection and background sizing
  accumulate without input, but no keypress means no marker ever (anti-AFK). If the key is unbound
  the plugin toasts once to point at the setting. Keypresses are ignored while a GUI control has
  keyboard focus (chat / text fields).
- **Marker cap per press:** one keypress creates at most N NEW markers (`Config.MaxMarkersPerPress`,
  default 5, slider 0-5, 0 = no limit) - the most recent candidates, by recency (monotonic
  per-detection sequence `s_seq` -> `FoundOre.Seq` -> `Component.Seq`, max over cluster members).
  The cap is applied in `Publish` AFTER clustering (a wide deposit or a whole small-ore field
  counts as ONE marker) and candidates that match an existing published marker (`FindMatch` -
  the same rule `HandleComponent` uses) bypass it: updates/merges/upgrades are never limited.
- **Vanilla-information limit (PluginHub requirement):** size figures must not expose more than
  vanilla knows. Rough sizes are fine, but yields are computed from a **baseline variant per ORE**
  (`VoxelScan.BaselineYield`: `<Ore>_01`, else ore-named material, else richest variant) - never
  from the actual voxel variant (the detector cannot tell Snow from Ice, Triton stone from Stone).
- No publicizer. Mode 1 is a manual Harmony target on the internal `MyOreDepositGroup.OnDepositQueryComplete`
  (resolved by name in `Plugin.Init`). Sizing uses public voxel APIs (`IMyStorage.ReadRange`,
  `MyStorageData`, `MyVoxelCoordSystems`, `MyDefinitionManager.GetVoxelMaterialDefinition`).
- C# `LangVersion=latest`, nullable disabled. Targets `net48` (Pulsar Legacy) and `net10.0` (Interim).
- Sizing scans run on a background thread, centred on each detected deposit's own position (so it
  works at any ship speed), one area per ~0.4 s cycle.

Key files:
- `ClientPlugin/Plugin.cs` — init + manual Harmony patch.
- `ClientPlugin/Patches/OreDepositPatches.cs` — Mode 1 postfix (the legit input).
- `ClientPlugin/AutoGpsService.cs` — detection gate, throttled sizing, connected-component
  clustering, field markers, GPS publish/de-dup.
- `ClientPlugin/VoxelScan.cs` — background LOD-2 voxel reader (positions + solid-voxel counts).
- `ClientPlugin/Config.cs` — settings dialog.

Build: `dotnet build OreToAutoGps.sln` — deploys to `%AppData%\Pulsar\Legacy\Local` and
`...\Interim\Local`. Author/repo: dawidmachon/se-ore-to-auto-gps.
