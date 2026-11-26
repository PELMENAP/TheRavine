# Ravine: world shape along z

Root: `Assets/Scripts/ObjectSystem/Map/WorldGeneration/`. Assembly: `ObjectSystem`. Namespace: `TheRavine.Generator`.
Tests: `Assets/Tests/EditMode/RavineProfileTests.cs`, `Assets/Tests/EditMode/ChunkStoreTests.cs`. Chunks and terrain: `docs/map/world.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `RavineProfile` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/RavineProfile.cs` | Elevation by world z: `Blend`, `Elevation`, `ElevationRange`, `IsValley`, `IsWall`, `IsPlateau` |
| `ChunkGenerationSettings` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkGenerationSettings.cs` | Header `Ravine`: `ravineHalfWidth`, `ravineWallRun`, `ravineWallHeight` |
| `MapGenerator` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/MapGenerator.cs`, `MapGenerator.Terrain.cs` | `Ravine` (one instance), `plateauSettings`, `AboveWater` |
| `RavineBackdrop` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/Rendering/RavineBackdrop.cs` | Analytic walls, plateau strip and valley floor beyond the streamed terrain |

## Data flow

1. `MapGenerator.SetUp` builds `Ravine` from the valley settings, then the valley `ChunkGenerator` and, unless the profile is flat, the plateau one.
2. `t = Blend(z) = saturate((|z| - halfWidth) / wallRun)`: 0 valley, between 0 and 1 wall, 1 plateau. `Elevation(z) = wallHeight * t`.
3. A chunk whose centre is on the plateau is built by the plateau generator; a chunk across the wall is a valley chunk.
4. Plateau settings without `regions` fall back to the valley settings with one warning; `endlessFlag` and `isRiver` always come from the valley.
5. Noise, biome and erosion jobs work on relief 0..1; `FillChunkArraysJob` writes `relief * maxTerrainHeight + Elevation(z)` to `ChunkData.HeightRaw`.
6. `CatchUpTerrain` strips the elevation (`ReliefFromHeightJob`), erodes, adds it back (`HeightFromReliefJob`); `HeightEdits` hold final heights.
7. `TerrainFinalizeJob` marks wall cells: region `mountainRegionIndex`, biome 0, move cost 0. `SpawnLayerJob` places nothing on them.
8. Every 0..1 normalisation by `maxTerrainHeight` subtracts `Elevation(z)` first: `TerrainSensor` height input, `SpawnLayerJob` height range.
9. Water: `MapGenerator.AboveWater(ravine, height, z, waterLevel)` is the height above the local water surface.
   Users: `WaterModifier`, `WaterDistanceJob`, `ChunkFoodIndex`, `IsHeightIsLiveAble`, `IsWaterHeight`.
10. `EndlessLiquids` lifts a block's water by the block's elevation and hides water on a block that contains wall.
11. Bounds of terrain blocks, grass windows, liquids and far objects are centred on `ElevationRange` of their block and cover its span.

## Backdrop

- Shader `Assets/Shaders/Terrain/RavineBackdrop.shader`; the profile on the GPU is `Assets/Shaders/Include/RavineProfile.hlsl`, global `_RavineProfile` set once in `MapGenerator.SetUp`.
- Created by `MapGenerator` as `endless[4]` with terrain on and a non-flat profile: `ravineBackdropMaterial`, else `ravineBackdropShader` + terrain material properties.
- One static grid mesh for one side (|z| from 0 to the plateau strip), drawn twice by `Graphics.RenderMesh` with `_BackdropSide` ±1; columns follow `Camera.main` along x in steps of `backdropCell`.
- The vertex shader takes height from `RavineElevation` plus value noise; fragments inside the terrain reach (`_BackdropReach`, Chebyshev from the window centre) are clipped.
- A vertex beyond `_PullStart` of the far plane is pulled along its view ray toward `_PullLimit`; the far plane is not changed. No shadows; no chunk data.
- Haze: `1 - exp(-backdropHazeDensity * true distance)` toward `unity_FogColor`. Settings: header `Ravine Backdrop` of `ChunkGenerationSettings`.
- Steep terrain: `BiomeTerrainCommon.hlsl` samples far rock in a side projection above `_RockSideSlope`; the near path is triplanar.

## Saving

- Chunk files store absolute heights and object y. Format 5 marks files written with the profile.
- Files of format below 5 are read only for chunks whose z range has no elevation; elsewhere they are refused like an unknown version.

## Invariants

- The profile depends on world z only; both generators share it, so height is continuous across the generator border up to relief.
- `ravineWallHeight = 0` is the flat world: no elevation, no wall cells, no plateau generator.
- Player and agents use the same `AboveWater` test and stand on `max(terrain, support)`; neither has a rule of its own on the wall.

## Hot paths

`docs/map/HOT_PATHS.md`, sections World and Rendering.
