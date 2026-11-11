# Hot paths

Code listed here runs per agent per tick or per frame. The constraints of `docs/OPTIMIZATION.md` section 2 are mandatory for it:
zero allocation, no LINQ, no closures, no `Transform` reads, `Unity.Mathematics`, no per-agent string work.

A file not listed here and not called from a method listed here is not hot. Menu, editor, loading, UI and debug views are never hot
(`docs/OPTIMIZATION.md` section 17). When a change adds a method to a tick, add it here in the same commit.

## Agent tick

Driver: `EntityManager.Update` in `Assets/Scripts/EntitySystem/AI/EntityManager.cs`. Order inside one frame:

1. `SimulationClock.Advance`.
2. Periodic, not per agent: `UpdateDiagnostics`, `DecayWarmth`, `SampleEnergy`, `SimulationLog.Sample`.
3. `TickAgents`: `TickWindowBudget.Take` decides how many agents tick this frame.
4. At the start of a window: `RebuildTickSnapshot` (snapshot, spatial grid, flocking).
5. `RunTickBatch`, first loop: `EntityModel.BeginCycle`, then `InstinctSystem.Write` and `UtilityPriorSystem.Write`.
6. `InstinctSystem.Schedule` and `UtilityPriorSystem.Schedule`, combined, one `Complete`.
7. Second loop: `ApplyUtilityPrior`, `ApplyInstinct`, `SubmitDecision`.
8. `SharedHierarchicalBrain.RunDecisions` for every colony.
9. Third loop: `EntityModel.EndCycle`.
10. `MovePlanner.Flush`, then `InfectionCoordinator.Spread` on the batch.
11. At the end of a window, `CloseTickWindow`: deaths, extinctions, predators, `ApplyPendingGradients`, nests, castes, visibility.
12. Every frame after the tick: `FireTickHandler.Step`, `FireView.Render`, `SpeechBus.Flush`, `MovePlanner.Flush`, `MotionSystem.Step`.

| Area | File | Hot methods |
|---|---|---|
| Tick driver | `Assets/Scripts/EntitySystem/AI/EntityManager.cs` | `Update`, `TickAgents`, `RunTickBatch`, `RebuildTickSnapshot`, `RunFlocking`, `CloseTickWindow` |
| Agent cycle | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.Cycle.cs` | `BeginCycle`, `RunCycle`, `EndCycle`, `SubmitDecision`, `StartDecision`, `ComputePlanHints` |
| Agent behaviour | `Assets/Scripts/EntitySystem/AI/Entity/` | Other `EntityModel.*.cs` partial files when called from the cycle |
| Agent commands | `Assets/Scripts/EntitySystem/AI/Entity/Commands/` | The tick method of every command; `EntityCommandRunner` |
| Plans | `Assets/Scripts/EntitySystem/AI/Plans/PlanRunner.cs` | `Tick`, `Advance`, `ResolveDestination` |
| Vectorisation | `Assets/Scripts/EntitySystem/AI/InputVectorizer.cs` | whole file |
| Metabolism | `Assets/Scripts/EntitySystem/AI/Entity/CreatureCore.cs` | `Metabolize`, `StepVirology`, `ReadVitals` |
| Virology | `Assets/Scripts/EntitySystem/Virology/VirologyComponent.cs`, `Assets/Scripts/EntitySystem/Virology/Ribosome.cs` | `VirologyComponent.Step`, `Ribosome.Step`, `Relax` |
| Infection | `Assets/Scripts/EntitySystem/Virology/InfectionService.cs` | `ProcessSpread` and the `TryTransmit` family |

## Decide, forward pass, train

| Area | File | Hot methods |
|---|---|---|
| Brain | `Assets/Scripts/EntitySystem/AI/SharedAI/SharedHierarchicalBrain.cs` | `EnqueueDecision`, `RunDecisions`, `GiveReward`, `CompleteDecision`, `ScheduleTraining`, `ApplyPendingGradients` |
| Kernels and jobs | `Assets/Scripts/EntitySystem/AI/SharedAI/NeuralKernels.cs`, `Assets/Scripts/EntitySystem/AI/SharedAI/NeuralJobs.cs` | whole files |
| Batch and contexts | `Assets/Scripts/EntitySystem/AI/SharedAI/` | `PerceptronBatch`, `EntityBrainContext`, `LSTMContext`, `PerceptronContext`, `DecisionRing`, `ContextSlabs`, `FloatSlabPool` |
| Networks | `Assets/Scripts/EntitySystem/AI/DelayedPerceptron/DelayedPerceptron.cs`, `Assets/Scripts/EntitySystem/AI/LSTM/` | forward and backward passes |
| Critic and novelty | `Assets/Scripts/EntitySystem/AI/SharedAI/ValueCritic.cs`, `Assets/Scripts/EntitySystem/AI/SharedAI/RandomNetworkDistillation.cs` | whole files |

## Perception, instincts, colony blackboard

| Area | File | Hot methods |
|---|---|---|
| Neighbours | `Assets/Scripts/EntitySystem/AI/Perception/EntitySpatialGrid.cs` | `Rebuild`, `Survey`, `FindNearest`, `FindInRadius` |
| Food | `Assets/Scripts/EntitySystem/AI/Perception/ChunkFoodIndex.cs` | `TryFindNearestFood`, `TryLocate`, `TryClaim`, `BiteMeat`, `TryTakePayload` |
| Terrain | `Assets/Scripts/EntitySystem/AI/Perception/TerrainSensor.cs`, `Assets/Scripts/EntitySystem/AI/Perception/TerrainSteering.cs` | whole files |
| Fields | `Assets/Scripts/EntitySystem/AI/Perception/WarmthField.cs`, `Assets/Scripts/EntitySystem/AI/Perception/PlayerPresence.cs` | `Read`, `Add`; `TryFindNearest` |
| Instincts | `Assets/Scripts/EntitySystem/AI/Instinct/InstinctSystem.cs` | `Write`, `Schedule`, `Output`, `InstinctJob` |
| Utility | `Assets/Scripts/EntitySystem/AI/Utility/UtilityPriorSystem.cs` | `Write`, `Schedule`, `Output`, `UtilityPriorJob` |
| Flocking | `Assets/Scripts/EntitySystem/AI/Flock/BoidSystem.cs` | `Write`, `Run`, `Output`, `BoidJob` |
| Castes | `Assets/Scripts/EntitySystem/AI/Caste/CasteThresholdSystem.cs` | `Get`, `Reinforce` |
| Colony fields | `Assets/Scripts/EntitySystem/AI/Nest/NestState.cs` | `Mark`, `MarkPair`, `FieldAt`, `SampleDirection`, decay and shift jobs |
| Points of interest | `Assets/Scripts/EntitySystem/AI/Colony/ColonyPoiBoard.cs` | `Offer`, `TryPickBest` |
| Speech | `Assets/Scripts/EntitySystem/AI/Speech/SpeechBus.cs` | `Emit`, `TryStartCooldown`, `InboxAt`, `Flush`, `SpeechDeliverJob` |

## Motion

| Area | File | Hot methods |
|---|---|---|
| Agent motion | `Assets/Scripts/EntitySystem/Job/MotionSystem.cs` | `Step`, `MotionJob`, `MotionApplyJob`, `PositionOf`, `SetPosition` |
| Move planning | `Assets/Scripts/EntitySystem/Job/MovePlanner.cs` | `Enqueue`, `Flush`, `PlanJob` |
| Obstacles | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/Collision/ObstacleQuery.cs` | whole file; called per agent per frame and per player per physics step |
| Mobs | `Assets/Scripts/EntitySystem/Entityes/Mob/MobController.cs` | `Update`, `LateUpdate`, `MoveMobsJob` |
| Mimic legs | `Assets/Scripts/EntitySystem/Entityes/Mimic/Scripts/MimicLegJob.cs` | whole file |

## Birds

`Assets/Scripts/EntitySystem/Job/BoidsBehaviour.cs` (`Update`, `LateUpdate`), `AccelerationJob.cs`, `MoveJob.cs`, `BirdHeightField.cs`,
`BirdPerchIndex.cs` in the same folder. Description: `docs/map/jobs.md`.

## World

| Area | File | Hot methods |
|---|---|---|
| Frame | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/MapGenerator.cs` | `Update`, `ApplyWorldCommands`, `FlushDirty`, `SupplySimSquare`, height and occupancy queries |
| Random ticks | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/Ticks/RandomTickService.cs` | `Update`, `Tick`, `Dispatch`; `RandomTickJob` |
| Tick handlers | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/NAL_PC.cs`, `Assets/Scripts/EntitySystem/AI/Hazards/FireTickHandler.cs` | `OnRandomTick`, `Step` |
| Fire rendering | `Assets/Scripts/EntitySystem/AI/Hazards/FireView.cs` | `Render`, `Rebuild`, `UpdateLights` |
| Chunk jobs | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/ChunkJobs/` | Burst jobs; budgeted by `FrameBudget`, not per frame |

## Rendering

`FarObjectRenderer`, `GrassSystem`, `EndlessTerrain`, `EndlessLiquids`: per-frame `Tick` and `Render`. Shader properties by cached id only.

## Server request path

RPC handlers in `Assets/Scripts/EntitySystem/Entityes/Player/PlayerController.cs`: no allocation and no formatting on the success path.

## Guards

- `AllocationAssert.Zero` in `Assets/Tests/EditMode/AllocationAssert.cs`; used by `BrainAllocationTests`.
- `SoaSwapBackTests` covers every per-agent SoA system; the registry is `docs/OPTIMIZATION.md` section 14.
- New hot code gets an allocation test; a new SoA system gets a swap-back test.
