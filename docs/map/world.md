# World: chunks, terrain, objects, ticks, rendering

Root: `Assets/Scripts/ObjectSystem/`. Assembly: `ObjectSystem`. Namespace: mostly `TheRavine.Generator`.
Tests: `Assets/Tests/EditMode/` — `ChunkStoreTests`, `ChunkPlacementTests`, `BuildingVolumeTests`, `FootprintTests`, `NalRegionTests`,
`RandomTickJobTests`, `RandomTickMathTests`, `WorldCommandBufferTests`.

Do not read `Assets/Scripts/ObjectSystem/Map/Tools/FastNoiseLite.cs` (vendored).
`MapGenerator.cs` and `ChunkGenerator.cs` are over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `MapGenerator` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/MapGenerator.cs` | Chunk window, chunk supply and eviction, object placement, height queries |
| `ChunkData` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/MapGenerator.cs` | One chunk: heights, objects, level mask, buildings, dirty flags |
| `ChunkGenerator` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkGenerator.cs` | Builds a chunk with the Burst jobs of `Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkJobs/` |
| `ChunkStore` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkStore.cs` | Saves and restores modified chunks |
| `WorldCommandBuffer` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs` | Queue of world changes; also `WorldEdits`, `WorldEditResult` |
| `RandomTickService` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/Ticks/RandomTickService.cs` | Random ticks over the simulated square |
| `NalTickHandler` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/NAL_PC.cs` | Object ecosystem (NAL) on random ticks |
| `ObjectSystem` | `Assets/Scripts/ObjectSystem/ObjectSystem.cs` | Registry of placed object instances; pooling through `PoolManager` |
| `ObjectInfoRegistry` | `Assets/Scripts/ObjectSystem/ObjectInfoRegistry.cs` | Catalogue of all `ObjectInfo` |
| `FarObjectRenderer` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/Rendering/FarObjectRenderer.cs` | Instanced far-LOD objects and billboards |
| `FrameBudget` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/FrameBudget.cs` | Deadline inside a frame |

## Grid

- Constants in `MapGenerator`: `mapChunkSize` (cells per side), `scale` (world units per cell), `chunkSize`, `WindowSide`.
- Coordinate helpers: `WorldToChunk`, `WorldToLocalIdx`, `CellToChunk`, `ChunkForCell`. Cell and chunk keys: `Position2Int.Pack`.
- Noise: static `Noise` in `Assets/Scripts/ObjectSystem/Map/Tools/Noise.cs`; settings in `NoiseLayerSettings.cs` next to it.
- Chunk jobs: `BiomeModifierJob`, `HydraulicErosionJob`, `TerrainFinalizeJob`; object spawn: `SpawnLayerJob` in the ObjectGenerator folder.

## Frame flow

1. `MapGenerator.Update` moves the window when the viewer's chunk changes (`MoveWindow`).
2. It applies queued world changes (`ApplyWorldCommands`), then runs `RandomTickService` where `WorldAuthority.IsAuthority`.
3. Dirty chunks are flushed once per frame (`FlushDirty`).
4. Missing chunks of the simulated square are supplied by `SupplySimSquare`, inside out, with half of the remaining `FrameBudget`.
5. Each endless layer gets `Tick(deadline)`; then `ProcessChunkRequests(deadline)`.
6. Far chunks are evicted (`EvictChunks`) unless they need saving, were used recently, or `IsRetained`.

## Endless layers (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/IEndless/`)

- `EndlessTerrain`: central window mesh with collider, ring blocks along the viewing axis, two side blocks of ring 0.
- `EndlessLiquids`: LOD water blocks on the same rings, index-based edge stitching.
- `EndlessObjects`: reuses object instances through `ObjectSystem`.
- Mesh jobs: `TerrainMeshJobs.cs`. Grass: `GrassSystem` in the GrassGenerator folder. Ripples: the RippleSystem folder.

## Camera view

- `ViewFacing` (`Assets/Scripts/Model/BehavioursMap/View/ViewFacing.cs`): four sides; helpers `Forward()`, `Right()`, `Yaw()`, `Rotate`, `Opposite()`.
- `ViewOrientation`: single source of the view; written only by the local `CameraComponent`; event `FacingChanged`.
- On a change it sets shader globals `_ViewBasis` and `_ViewColumn`; shader side in `Assets/Shaders/Include/ViewOrientation.hlsl`.
- `MapGenerator.ViewRingOf` / `ViewRingOfChunk` describe only what the camera draws. `SimRingOf` describes what is simulated.
- Visibility is a half-space along `Forward()` plus a lateral window along `Right()`; no frustum test.
- World bend: `Assets/Shaders/Include/CurvedWorld.hlsl`, pushed by `CurvedWorld` in the Rendering folder.
- A view-dependent component implements `IViewOriented` and registers with `ViewOrientation`; a plain transform uses `ViewOrientedTransform.Attach`.
- Far billboards: `ObjectLodSet.BillboardSideCount`; atlas baked by `Assets/Scripts/Editor/TreeMeshBaker.cs`.

## Objects

- `ObjectInfo` (`Assets/Scripts/ObjectSystem/ObjectInfo/ObjectInfo.cs`): data of an object kind; `BehaviourType` is `None`, `NAL` or `GROW`.
- Multi-level buildings: `ObjectInfo.Levels` up to `ObjectInfo.MaxLevels`; `ChunkData.LevelMask` is one byte per cell.
- `ChunkData.TryGetBuilding` returns a `BuildingVolume` only for buildings with more than one level.
- Occupancy queries: `MapGenerator.IsOccupiedAt`, `ChunkData.HasLevelAt`. Footprints: `Footprint.cs`. Placement results: `PlacementResult.cs`.
- Spread on death or pick-up: `SpreadPattern` in `Assets/Scripts/ObjectSystem/ObjectInfo/SpreadPattern.cs`.
- Trees: `Assets/Scripts/ObjectSystem/TreeGenerator/`. Wind: `WindField` and `WindSampler` in `Assets/Scripts/ObjectSystem/Wind/WindField.cs`.
- Structures: `WaveFunctionCollapseAlgorithm` and `StructureGenerator` in the StructGenerator folder, rules in `TileRuleSO`, `TilePatternSO`.

## Collision (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/Collision/`)

- No PhysX for world objects: a body is a circle with a vertical span, pushed out of object shapes found through `ChunkData.Occupancy`.
- Shape of an object kind is data in `ObjectInfo`: `CollisionKind` is `None`, `Cylinder` (radius and height in world units) or `Box`.
- A box is `collisionSteps` in build-grid steps: x and z times `buildStep`, y times `levelHeight` (both in `ChunkGenerationSettings`).
- `collisionOffsetSteps` moves the shape from the object position, in the same steps. The shape bottom is the object's y.
- `CollisionTable` bakes every `ObjectInfo.BakeCollision` into `CollisionShape` by object id; `MapGenerator.Collision` owns it.
- `ObstacleQuery.Resolve` is the only query; it is generic over `IObstacleLookup` and Burst-compatible.
- Callers: `MapGenerator.ResolveObstacles` (player, through `TerrainPhysicsResolver`) and `MotionJob` (agents, through `HeightAtlas`).
- Ground objects are found by their primary cell; objects with a base level above zero by a scan gated by `ChunkData.ElevatedCount`.
- The search covers at most `ObstacleQuery.MaxCellReach` cells around the body; a larger shape extent is not found beyond it.
- A body whose feet are at or above the shape top is not blocked; standing on a shape is not implemented.

## Random ticks and NAL (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/Ticks/`)

- Every `WorldTickRules.tickSeconds` of `SimulationClock` time, `RandomTickJob` samples `cellsPerChunkPerTick` cells per chunk of the square.
- The square is the window out to `WorldTickRules.nalRings` rings; it does not depend on the camera view.
- Far rings get fewer cells (`farCellDivisor`). The tick cost does not depend on the number of objects.
- Hits on primary object cells go to `IRandomTickHandler` subscribers: `NalTickHandler`, `FireTickHandler`.
- A chunk that re-enters the square catches up: up to `catchUpBurstRounds` extra rounds per tick, bounded by `catchUpMaxSeconds`.
- NAL probabilities: `NalRules` and `RandomTickMath`. Objects with `decaySeconds` above zero and no behaviour rot.
- Every world change made by a handler goes through `WorldCommandBuffer`.

## Saving

- `ChunkStore` writes chunks with `ChunkData.NeedsSave` to `worlds/{worldId}/chunks/` on autosave and on leaving the scene.
- On generation the file is applied on top of the procedural result (`TryRestore`).
- File: header with magic, version, seed, chunk key, then objects with their levels; writes through `AtomicFile`.
- Non-finite values or an oversized file reject the whole file; unknown object ids are skipped.

## Teardown

- `MapGenerator.BreakUp` (same as `OnDisable`): stops `Update`, unsubscribes, saves modified chunks, disposes the endless layers.
- Chunk data, the chunk pool, `ChunkGenerator`, random ticks and `Commands` are disposed in `OnDestroy`, when nothing can query them.

## Invariants

- Simulation, perception and rules never depend on the view; only drawing does.
- Per-frame generation work takes the `FrameBudget` deadline and stops when it expires.
- Random ticks and world edits run only on the authority.
- Loaders follow `docs/SECURITY.md` section 7.
- Player and agents collide with the same shapes through the same `ObstacleQuery.Resolve`; neither has its own obstacle rules.
- `ChunkData.ElevatedCount` equals the number of objects with a base level above zero; every add, remove and clear keeps it.

## Hot paths

`docs/map/HOT_PATHS.md`, sections World and Rendering.

## Open issues

- World edits are replicated only to the editing client (audit finding 27).
