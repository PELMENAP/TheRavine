# Jobs: agent motion, move planner, birds, menu boids

Root: `Assets/Scripts/EntitySystem/Job/`. Assembly: `EntitySystem`. Namespace: mostly `TheRavine.EntityControl`.
Tests: `Assets/Tests/EditMode/SoaSwapBackTests.cs`, `Assets/Tests/EditMode/MimicLegJobTests.cs`.

Job rules (attributes, allocators, batch sizes, one `Complete` per batch): `docs/OPTIMIZATION.md` section 15.
`BoidsBehaviour.cs` and `MotionSystem.cs` are over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `MotionSystem` | `Assets/Scripts/EntitySystem/Job/MotionSystem.cs` | Moves agents along planned curves over terrain; owns agent positions |
| `MovePlanner` | `Assets/Scripts/EntitySystem/Job/MovePlanner.cs` | Batches move requests into `PlanJob`; reads colony fields |
| `BoidsBehaviour` | `Assets/Scripts/EntitySystem/Job/BoidsBehaviour.cs` | Bird flocks: orchestrator of the bird job chain |
| `BoidsInfo` | `Assets/Scripts/EntitySystem/Job/BoidsInfo.cs` | Bird settings (ScriptableObject) |
| `MobController` | `Assets/Scripts/EntitySystem/Entityes/Mob/MobController.cs` | Mob movement through `MoveMobsJob`; obstacles and ground from `MotionSystem.PrepareAtlas` |
| `MimicLegSystem` | `Assets/Scripts/EntitySystem/Entityes/Mimic/Scripts/MimicLegSystem.cs` | Runs `MimicLegJob` once per frame from `MimicLegDriver` |
| `BoidsMenuBackground` | `Assets/Scripts/GameSceneManagerSystem/Menu/BoidsMenuBackground.cs` | GPU boids behind the main menu |

The flocking of agents (not birds) is `BoidSystem` in `Assets/Scripts/EntitySystem/AI/Flock/BoidSystem.cs`: `docs/map/ai.md`.

## Agent motion

1. Commands call `MovePlanner.Enqueue`; `MovePlanner.Flush` runs `PlanJob` for the batch (`PlanRequest` → `PlanResult`).
2. Results start a motion in `MotionSystem` (`Start`, `Stop`); state per agent is `MotionState`.
3. `MotionSystem.Step` runs `MotionJob` (integration over `HeightAtlas`) and `MotionApplyJob` (the only writer of agent transforms).
4. After the step along the curve `MotionJob` calls `ObstacleQuery.ResolveSwept` from the previous to the new position; a pushed agent catches up at `ObstacleCatchUpMul` of its speed.
   A push that leaves almost no progress counts toward `BlockedSeconds`. Body size: `AgentBodyRadius`, `AgentBodyHeight`; the step-up tolerance is `AgentStepUp`.
   The height is `max(terrain, ObstacleQuery.Support)` plus `HeightOffset`: an agent stands on a shape top and drops to the terrain off its edge.
5. Positions are read with `MotionSystem.PositionOf`; nothing in the tick reads a `Transform`.
6. `MotionSystem.RemoveSwapBack` is called from `ColonyPopulation.RemoveEntitySwapBack`.
7. `HeightAtlas` is synced from loaded chunks (`PrepareAtlas`, `SyncAtlas`); it is rebuilt on change, not every frame.
   `MotionSystem` is a service: `EntityManager` registers it, `MobController` reads its atlas and `Obstacles`.
8. `MobController.Update` fills the mob velocities, prepares the atlas around the mob positions, schedules `MoveMobsJob` and completes it in the same call:
   the atlas arrays belong to `MotionSystem` and change later in the frame. `MoveMobsJob` resolves each mob like an agent and sets `y` from ground plus `GroundOffset`.
   Mob body: `EntityInfo.BodyRadius`, `BodyHeight`, `GroundOffset`; step-up is `AgentStepUp`. Without `MotionSystem` the mobs hold their position.

## Birds

- State is SoA `NativeArray` indexed by bird; birds of a flock occupy a contiguous range. The population is fixed: no removal.
- Per frame, completed in `LateUpdate`: `BirdSlotJob` → `BirdGridJob` → `AccelerationJob` → `MoveJob`.
- `BirdSlotJob`: frees perch slots and plans a flock's landing. `BirdGridJob`: counting-sort hash grid.
- `AccelerationJob`: flocking over 3×3 buckets with a neighbour limit, target seeking, height regulation, tree avoidance, panic.
- `MoveJob` (`IJobParallelForTransform`): flight, landing, perching, take-off, sprite facing.
- States (`BirdState`): flying, approach, landing, perched, take-off.
- `BirdHeightField` (`Assets/Scripts/EntitySystem/Job/BirdHeightField.cs`): height window around the observer; rebuilt when the observer moves.
- `BirdPerchIndex` (`Assets/Scripts/EntitySystem/Job/BirdPerchIndex.cs`): trees as perches with a 64-bit slot mask; `PerchRemapJob` on rebuild.
- Shared parameters: `BirdParams` in `Assets/Scripts/EntitySystem/Job/BirdData.cs`, snapshot of `BoidsInfo` taken in `StartBoids`.
- Wind: `WindSampler` from `Assets/Scripts/ObjectSystem/Wind/WindField.cs`.
- Time: `SimulationClock` only, delta capped by `BoidsInfo.maxDeltaTime`.
- Lifecycle: `EntitySystem.SetUpBoids` → `StartBoids`; `EntitySystem.BreakUp` → `DisableBoids`; `OnDestroy` frees every native container.

## Menu boids

- Fully on GPU: `Assets/Shaders/Boids/BoidsStep.compute`; drawn with `Graphics.RenderMeshIndirect`.
- Time is `Time.unscaledDeltaTime`: `SimulationClock` does not advance in the menu scene.

## Invariants

- A perch slot bit is set if and only if exactly one bird holds that reference; checked by `Debug.Assert` in the editor.
- A parallel job writes only its own index; neighbour reads use the previous state.
- `Complete` is called once per batch, never inside a per-agent loop.
- Every `NativeArray` and `TransformAccessArray` is disposed in `OnDestroy` or `Dispose`, with an `IsCreated` check.

## Hot paths

Everything in this file. `docs/map/HOT_PATHS.md`, sections Motion and Birds.

## Open issues

None recorded.
