# Landforms: local terrain, river and erosion rules

Root: `Assets/Scripts/ObjectSystem/Map/`. Assembly: `ObjectSystem`. Namespace: `TheRavine.Generator`. Tests: `Assets/Tests/EditMode/LandformBlendTests.cs`.
Read `docs/map/world.md` first; do not read `Assets/Scripts/ObjectSystem/Map/Tools/FastNoiseLite.cs` (vendored).

## Data

- `LandformSettings` (`Assets/Scripts/ObjectSystem/Map/Tools/NoiseLayerSettings.cs`): centre in (continentalness, ruggedness), height scale and offset, ridge, warp, terraces, river width, carve and warp, erosion multiplier.
- `LandformPresets.All` (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkJobs/LandformPresets.cs`): `Hills` is the identity and is first.
- `ChunkGenerationSettings`: `landforms`, `continentalnessSettings`, `ruggednessSettings`, `ridgeNoiseSettings`, `landformWarpSettings`, `landformBlendRadius`, `landformBaseWeight`, `landformContrast`.
- `ChunkGenerator` bakes `landforms` into a persistent `NativeArray<LandformParams>` and owns `riverShapeMap` and `erosionScaleMap`.

## Flow

- `Noise.HeightRiverMapJob` runs per cell at full resolution, no 2x2 duplication: `LandformBlend.Control` gives (c, r) with contrast, `LandformBlend.Evaluate` gives `LandformParams`.
- The same job warps coordinates by one `DomainWarp` call (skipped when warp and river warp are zero), mixes the ridge noise, terraces, then applies scale and offset.
- `LandformBlend.Evaluate` uses the smoothstep kernel of `BiomeModifierJob`; only the difference from identity is summed, so identity and an empty array stay exact and there is no fallback branch.
- `BiomeModifierJob` reads `riverShape`: river band radius times `riverWidth` (0 means no river), river blend times `riverCarve`.
- `HydraulicErosionJob` multiplies its delta by `erosionScale`; droplets are unchanged.
- `CatchUpTerrain` has no noise maps: `Noise.LandformErosionJob` refills only `erosionScaleMap`, inside the `ErosionBudget`; `ChunkData` and `ChunkStore` do not hold it.

## Invariants

- Water level stays global (`SimulationRules.WaterLevel`).
- `Generate` and `CatchUpTerrain` produce the same erosion scale for the same chunk: both call `LandformBlend.Control` and `LandformBlend.Evaluate`.
- Noise parameters cannot vary per cell; local variation comes from blending the outputs of a fixed set of noise fields.
