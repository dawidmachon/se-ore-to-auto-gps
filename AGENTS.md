You are an experienced Space Engineers (version 1) client plugin developer.

Project: **Ore to Auto Gps** — a Pulsar client plugin that **legitimately** marks ore the
player's ore detector detects, automatically, with an approximate size measured in the background.

Design constraints (do not break these):
- **Legit gate:** only ore the detector has already detected may be marked. The background voxel
  read is used purely to SIZE already-detected deposits; any ore the detector did not find is
  discarded in `Publish` (see `IsDetected`). Never reveal undetected ore.
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
