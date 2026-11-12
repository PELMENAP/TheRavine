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

- `EndlessTerrain`: central window mesh with collider and a pool of block meshes for the blocks of the current view sector.
- `EndlessLiquids`: LOD water blocks on the same blocks, index-based edge stitching against a coarser neighbour on any world side.
- `EndlessObjects`: reuses object instances through `ObjectSystem`.
- `EndlessObjects` animates only an existing view whose `ChunkData.Version` changed; streaming a new view never animates.
- `ObjectViewMotion` (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/IEndless/ObjectViewMotion.cs`): LitMotion slots, fixed budget; over budget a change is instant.
- A new object pops in, a removed one shrinks and is then released; `ReleaseView` cancels motions and releases at once. `PooledObject.BaseScale` is restored in `Pool.Get`.
- NAL growth: a `BehaviourType.GROW` object whose cell turns into its `EvolutionStep` keeps its pooled instance and scales to the next size over `growthDuration`, then the next stage is taken.
- Growth ratio: `ObjectGrowthMath.Resolve`: renderer bounds per axis (cached per pool pair), else `CollisionTable` heights, else 1. The data already holds the next stage.
- Soil and trample on grass: `GrassInstancedShader.shader` reads `SoilShaderFeed` (`_SoilBuffer`) and `GrassTrampleField` (`_GrassTrample`) at render time; placement is untouched.
- `GrassSoilBuffer` owns `_SoilBuffer` and keeps a neutral one bound while `GrassSystem` runs, because D3D12 skips draws with an unbound SRV; `SoilShaderFeed` only fills it.
- `GrassTrampleField`: window-aligned R16 texture, compute decay and stamps from a fixed-capacity buffer; hosted by `GrassPlayersManger`, found by `ServiceLocator`, recentred by `GrassSystem`.
- Every creature stamps through `GrassTrampleField.StampCreature`: `EntityManager` for agents, `PlayerController` for players. Colony trails are not written into it.
- Mesh jobs: `TerrainMeshJobs.cs`. Grass: `GrassSystem` in the GrassGenerator folder. Ripples: the RippleSystem folder.

## Camera view

- `ViewSector` (`Assets/Scripts/Model/BehavioursMap/View/ViewSector.cs`): 16 sectors of 22.5° with 3° hysteresis, each with a central direction; 0 is +Z, 4 is +X.
- `ViewOrientation`: single source of the view (continuous yaw and pitch, `Forward`, `Right`, `ToWorld`); written only by the local `CameraComponent`. `ViewChanged` fires on every change, its listeners must be O(1); `SectorChanged` fires on a sector change, for heavy consumers.
- Shader globals: `_ViewBasis` (continuous) and `_ViewColumn` (`ViewOrientation.ColumnOf`, nearest of `BakedSides` baked sides); shader side in `Assets/Shaders/Include/ViewOrientation.hlsl`.
- Blocks sit on a world-anchored lattice: offset (bx, bz) from the window centre in `WindowSide` chunks; the LOD is the Chebyshev ring, equal to `SimRingOf`. A turn never changes the LOD or the mesh of a block.
- `ViewWedge` picks the blocks of a sector. With f along and l across the central direction a block is drawn if f >= -0.75 and l <= 1.5 + tan(52° + 14.25°) * f. The half-angle is the ground half-angle of the frame (vertical FOV 50°, aspect up to 2.4) plus half a sector and the hysteresis; a wider screen needs a larger `FrustumHalfDegrees`.
- `MapGenerator.ViewRingOf` / `ViewRingOfChunk` describe only what the camera draws. `SimRingOf` describes what is simulated and does not depend on the sector.
- `IViewDirectional.SetSector`: `EndlessTerrain` and `EndlessLiquids` build only the blocks that entered (nearest rings and nearest to the view axis first); blocks that left are hidden and keep their mesh in the pool. `EndlessTerrain.BuiltBlocks` counts builds.
- `FarObjectRenderer` rebuilds its instance buffer on a sector or window change at its existing rate limit; the pool, `sources` and the `farMaxInstances` budget are sized from `ViewWedge.MaxBlocks`.
- `GrassSystem` lays out the whole window and re-places it only when the window centre changes; grass does not depend on the view sector.
- World bend: `Assets/Shaders/Include/CurvedWorld.hlsl` bends radially around the camera pivot (`_CurveParams`, `_CurveFocus`), pushed by `CurvedWorld`; the focus is `ViewOrientation.Pivot`.
- A view-dependent component implements `IViewOriented` (called on every `ViewChanged`, O(1)) and registers with `ViewOrientation`; a plain transform uses `ViewOrientedTransform.Attach`.
- Far billboards: `BakedSides` sides in a circle (column k looks with yaw k * 360 / N); atlas baked by `Assets/Scripts/Editor/TreeMeshBaker.cs`; atlases baked with another side count must be re-baked.

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
- `ObstacleQuery` is the only collision for world objects: `Resolve`, `ResolveSwept` and `Support`. All are generic over `IObstacleLookup` and Burst-compatible.
- One cell walk serves all three: `ObstacleQuery.Walk` visits each shape found, a private visitor struct decides what to do with it.
- `Resolve` pushes the body circle out of shapes. A shape whose top is not above `feetY + stepUp` is not blocking: the body steps onto it.
- `Support` returns the highest shape top under the body circle that is not above `feetY + stepUp`; ground for a body is `max(terrain height, support)`.
- `ResolveSwept` splits a move into sub-steps no longer than the body radius, resolves after each, and stops at the first hit.
  A move not longer than the radius costs one `Resolve`. More than `MaxSweepSteps` sub-steps stop at the last resolved position (`SweepReach`).
- Callers: `MapGenerator.ResolveObstacles` and `MapGenerator.SupportObstacles` (player, through `TerrainPhysicsResolver`),
  `MotionJob` (agents, through `HeightAtlas`), `MoveMobsJob` (mobs, through the same `HeightAtlas`).
- Ground objects are found by their primary cell; objects with a base level above zero by a scan gated by `ChunkData.ElevatedCount`.
- The search covers at most `ObstacleQuery.MaxCellReach` cells around the body; a larger shape extent is not found beyond it.
- Tree and prop prefabs carry no `CapsuleCollider`; `CollisionCapsuleMigration` (`Assets/Scripts/Editor/CollisionCapsuleMigration.cs`) converts them to shape data.
- A platform is an `ObjectInfo` with a shape and no `ObjectPrefab`: no pool, no near view, and `FarObjectRenderer` skips it while `LodSet` is empty.
  A branch is such an object placed at a base level above zero on a tree's cell.

## Random ticks and NAL (`Assets/Scripts/ObjectSystem/Map/WorldGeneration/Ticks/`)

- Every `WorldTickRules.tickSeconds` of `SimulationClock` time, `RandomTickJob` samples `cellsPerChunkPerTick` cells per chunk of the square.
- The square is the window out to `WorldTickRules.nalRings` rings; it does not depend on the camera view.
- Far rings get fewer cells (`farCellDivisor`). The tick cost does not depend on the number of objects.
- Hits on primary object cells go to `IRandomTickHandler` subscribers: `NalTickHandler`, `FireEvent` (map events: `docs/map/map-events.md`).
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
