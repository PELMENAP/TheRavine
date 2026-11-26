# World: camera rings and rendering

Root: `Assets/Scripts/ObjectSystem/`. Assembly: `ObjectSystem`. World map: `docs/map/world.md`.
Tests: `Assets/Tests/EditMode/` — `GrassLodBandsTests`.

## Camera view

- `ViewSector` (`Assets/Scripts/Model/BehavioursMap/View/ViewSector.cs`): 16 sectors of 22.5° with 3° hysteresis, each with a central direction; 0 is +Z, 4 is +X.
- `ViewOrientation`: single source of the view (continuous yaw and pitch, `Forward`, `Right`, `ToWorld`); written only by the local `CameraComponent`.
  `ViewChanged` fires on every change, its listeners must be O(1); `SectorChanged` fires on a sector change, for heavy consumers.
- Shader globals: `_ViewBasis` (continuous) and `_ViewColumn` (`ViewOrientation.ColumnOf`, nearest of `BakedSides` baked sides); shader side in `Assets/Shaders/Include/ViewOrientation.hlsl`.
- Blocks sit on a world-anchored lattice: offset (bx, bz) from the window centre in `WindowSide` chunks; the LOD is the Chebyshev ring, equal to `SimRingOf`.
  A turn never changes the LOD or the mesh of a block.
- `ViewWedge` picks the blocks of a sector.
  With f along and l across the central direction a block is drawn if f >= -0.75 and l <= 1.5 + tan(52° + 14.25°) * f.
  The half-angle is the ground half-angle of the frame (vertical FOV 50°, aspect up to 2.4) plus half a sector and the hysteresis; a wider screen needs a larger `FrustumHalfDegrees`.
- `MapGenerator.ViewRingOf` / `ViewRingOfChunk` describe only what the camera draws. `SimRingOf` describes what is simulated and does not depend on the sector.
- `IViewDirectional.SetSector`: `EndlessTerrain` and `EndlessLiquids` build only the blocks that entered (nearest rings and nearest to the view axis first);
  blocks that left are hidden and keep their mesh in the pool.
  `EndlessTerrain.BuiltBlocks` counts builds.
- `FarObjectRenderer` rebuilds its instance buffer on a sector or window change at its existing rate limit; the pool, `sources` and the `farMaxInstances` budget are sized from `ViewWedge.MaxBlocks`.
- Far types with `CastShadowsFar` get two batches: within `ChunkGenerationSettings.farShadowDistance` of the window centre they cast shadows, beyond it they do not.
- `GrassSystem` ring 0: heights are uploaded when the window centre changes; the placement compute re-runs when `Camera.main` moves by `replaceMoveStep` or turns by `replaceTurnDegrees`.
- Ring 0 placement culls by the camera frustum (`cullMargin`); with `drawDistance` above zero it also thins density to zero at that distance. Far blocks are not culled this way.
- Ring 0 casts grass shadows only within `shadowDistance` of the camera (`_GrassView`, tested in the `ShadowCaster` vertex stage).
- Grass rings 1 to `GrassLodBands.FarRings`: one pool slot per wedge block (`ViewWedge.MaxBlocksInRing`), on the same block lattice as the terrain and following the view sector.
- A far block runs the same `PlaceGrass` kernel on a coarser cell grid (`lodCellSteps`, 4 and 8 units) with wider blades (`lodWidthScales`); buffer size is `GrassLodBands.Capacity`.
- After a window-centre change far blocks are rebuilt nearest first, `lodBlocksPerFrame` per frame, once `MapGenerator.TryBuildHeightWindow` finds all chunks;
  blocks leaving the wedge keep their buffer.
- One draw for the window and one per built far block; mesh by ring from `lodMeshes` (empty falls back to `grassMesh`); only ring 0 casts shadows; beyond the last grass ring there is no grass.
- The grass LOD is drawing only: placement rules, trample and soil do not depend on it. Far grass lies outside `GrassTrampleField`, whose texture clamps at its edge.
- No world bend: geometry is drawn where it is. Beyond the streamed terrain the walls come from `RavineBackdrop` (`docs/map/ravine.md`).
- A view-dependent component implements `IViewOriented` (called on every `ViewChanged`, O(1)) and registers with `ViewOrientation`; a plain transform uses `ViewOrientedTransform.Attach`.
- Far billboards: `BakedSides` sides in a circle (column k looks with yaw k * 360 / N); atlas baked by `Assets/Scripts/Editor/TreeMeshBaker.cs`; atlases baked with another side count must be re-baked.
