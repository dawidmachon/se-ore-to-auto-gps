# Ore yield / ingot potential — how it's calculated

Reference for the `~X kg ore -> ~Y kg ingots @100%` GPS figures: the maths, where every factor
comes from in the game files, and the quirks. So this can be understood, validated, and tuned later.

## Formula

For a scanned deposit of `V` solid ore voxels (≈ `V` m³, since 1 voxel = 1 m³):

```
oreKg   = V × 0.009 × serverHarvestMultiplier × MinedOreRatio × oreDensity
ingotKg = oreKg × (blueprint.Result.Amount / blueprint.Prerequisite.Amount)   # a MASS ratio
```

`@100%` = a default refinery with **no yield-upgrade modules**. Refinery *speed* multipliers
(server or block) change how **fast**, not how much per ore. The **drill-tier** multiplier is
unknown for a "potential" figure, so a **default drill (×1)** is assumed.

## Where each factor lives

| Factor | Game source | How read at runtime | Example (Silicon) |
|---|---|---|---|
| base harvest `0.009` | `Sandbox.Game` → `MyDrillBase.cs` (`VoxelHarvestRatio = 0.009f`) | hardcoded constant | 0.009 |
| server harvest × | `MySession.Static.Settings.HarvestRatioMultiplier` | live, per server | 1.0 default |
| `MinedOreRatio` | `Content/Data/VoxelMaterials_asteroids.sbc` (per material) | `MyVoxelMaterialDefinition.MinedOreRatio` | 3 (`Silicon_01`) |
| ore density kg/m³ | `Content/Data/PhysicalItems.sbc` → `Mass / Volume` | `GetPhysicalItemDefinition(id).Mass / .Volume` | 1 / 0.00037 = **2703** |
| blueprint ratio | `Content/Data/Blueprints.sbc` → `Result.Amount / Prerequisites.Amount` | `GetBlueprintDefinitions()` (match `InputItemType == MyObjectBuilder_Ore`) | 0.7 / 1.0 = **0.7** |

Implemented in `ClientPlugin/VoxelScan.cs` → `ComputeYield(...)`. The density/ratio are looked up
live via `MyDefinitionManager.Static` (`GetPhysicalItemDefinition`, `GetBlueprintDefinitions`).

## Worked example (Silicon, default server, default drill)

A node of ~89 voxels:
- `oreKg   = 89 × 0.009 × 1 × 3 × 2703 ≈ 6,500 kg` (inventory showed **6,482.86 kg** ✓)
- `ingotKg = 6,500 × 0.7 ≈ 4,550 kg` (refined to **4,538.00 kg** ✓)

## Size tiers (GPS name: Trace / Small / Medium / Large / Huge)

Based on **ore kg** (see `SizeWord` in the service). Tunable:
`Trace <10k · Small <100k · Medium <1M · Large <5M · Huge ≥5M` kg ore.

## Quirks / gotchas

- **Item Volume unit:** the game UI shows ore/ingot volume in **liters**, but
  `MyPhysicalItemDefinition.Volume` at runtime is in **m³** (e.g. silicon ore = 0.00037 m³ = 0.37 L).
  So density = `Mass / Volume` gives kg/m³ directly (don't multiply by 1000).
- **Blueprint Amount is a MASS ratio**, not volume — verified empirically (ore mass × ratio = ingot
  mass). Don't multiply by the ingot density; multiply ore **kg** by the ratio.
- **`removedAmount/255`** in the harvest code is per drill tick; summed over a fully-mined voxel it
  totals 1, so "per voxel" = `0.009 × server × MinedOreRatio`.
- **Unknown / modded ores** are still marked (controlled by the `TrackModdedOres` setting).
- **Vanilla crash guard:** deleting a GPS crashes a vanilla multiline-text control
  (`MyGuiControlMultilineEditableText.GetCarriageOffset` indexes `m_text` out of bounds when the
  caret is left past a shortened text). Guarded by `ClientPlugin/Patches/MultilineTextCrashFix.cs`.

## Precision

The formula and all factors are exact (from the game's definitions). The only approximation is the
**voxel volume estimate** (`V`), derived from an LOD-2 solid-sample count (the same method the
game's own ore detector uses). If a fully-mined node's GPS figure disagrees with reality, the gap
is in `V`, not the conversion.
