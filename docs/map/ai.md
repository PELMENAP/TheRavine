# AI: agents, colonies, shared brain

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Rules for this folder: `Assets/Scripts/EntitySystem/AI/CLAUDE.md`.
Tests: `Assets/Tests/EditMode/`.
Brain: `BrainAllocationTests`, `BrainLearningTests`, `BrainArchitectureVersionTests`, `SnapshotVersionTests`, `DecisionRingTests`.
Tick and per-agent systems: `SoaSwapBackTests`, `TickWindowBudgetTests`, `RulesDefaultsTests`, `SignalMaskTests`.
Perception: `EntitySpatialGridSurveyTests`, `SurveySystemTests`, `FamiliarityTests`, `FoodLookupAllocationTests`.
Behaviour: `EvolutionDynamicsTests`, `PredispositionTests`, `CuriositySurplusTests`, `StartleTests`.

Sub-maps: `docs/map/ai-colony.md` (checkpoints, colonies), `docs/map/ai-skills-stress.md`, `docs/map/ai-experiments.md` (arms, logs, headless runs), `docs/map/codex.md`.
Tick order and hot methods: `docs/map/HOT_PATHS.md`. Existing structures to reuse: `docs/OPTIMIZATION.md` section 13.
Large files (`EntityManager.cs`, `SharedHierarchicalBrain.cs`, `ChunkFoodIndex.cs`, `SimulationRules.cs`, `EntityModel.cs`): `docs/map/LARGE_FILES.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `EntityManager` | `Assets/Scripts/EntitySystem/AI/EntityManager.cs` | MonoBehaviour: lifecycle, tick driver, colonies, visibility, brain saving |
| `EntityModel` | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.cs` | One agent; partial files below |
| `SoilShaderFeed` | `Assets/Scripts/EntitySystem/AI/Perception/SoilShaderFeed.cs` | 5x5 chunks of `ChunkFoodIndex.TryGetSoil` to the grass shader per `EcologyTickInterval`; owned by `EntityManager` |
| `SharedHierarchicalBrain` | `Assets/Scripts/EntitySystem/AI/SharedAI/SharedHierarchicalBrain.cs` | One brain per colony, weights shared by its agents |
| `SimulationRules` | `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.cs` | All simulation constants (ScriptableObject); `Frame` snapshot for the tick |
| `ColonyRegistry` | `Assets/Scripts/EntitySystem/AI/Colony/ColonyRegistry.cs` | Colonies by index and id; `Views` for jobs |
| `ColonyPopulation` | `Assets/Scripts/EntitySystem/AI/Population/ColonyPopulation.cs` | Spawn, death, reproduction, swap-back removal |
| `ActionCatalog` | `Assets/Scripts/EntitySystem/AI/ActionCatalog.cs` | Verb table, flags, goal and caste masks; enum in `EntityAction.cs` |
| `InputVectorizer` | `Assets/Scripts/EntitySystem/AI/InputVectorizer.cs` | Observation → input vector (`VectorSize`) |
| `TickWindowBudget` | `Assets/Scripts/EntitySystem/AI/TickWindowBudget.cs` | How many agents tick this frame |
| `UrgentTickQueue` | `Assets/Scripts/EntitySystem/AI/UrgentTickQueue.cs` | Damaged creatures due for an early tick; `EntityManager.PromoteUrgent` moves them to the front of the batch |

## Agent (`Assets/Scripts/EntitySystem/AI/Entity/`)

- `EntityModel` partial files: `.Cycle` (tick), `.Behaviour`, `.Social`, `.Directions`, `.Homeostasis`, `.Wild`, `.Fitness`, `.Skills`, `.Stress`.
- Components in `Assets/Scripts/EntitySystem/AI/Entity/Components/`: `BrainComponent`, `DigestionComponent`, `FitnessComponent`,
  `MortalityComponent`, `PerceptionComponent`, `PointsOfInterestComponent`, `SpeechComponent`, `StatsComponent`, `VisualCullingComponent`.
- Commands, one file each, in `Assets/Scripts/EntitySystem/AI/Entity/Commands/`; they run through `EntityCommandRunner`.
- One command per verb.
  `MoveToCommand` picks its behaviour from the decision's `DirectionKind`;
  `SignalCommand` from the goal of the decision;
  `GiveCommand` from the receiver;
  `TakeCommand` and `PutCommand` from the direction and the carried load.
- `ObjectSensor` finds portable fuel. Harvest can approach and pick it up from the map, then deposit it in the nest as heat.
- `CreatureCore`: metabolism, vitals and virology step shared by agents and the player.
- `EntityTuning`: per-agent multipliers by `Caste` (`Worker`, `Soldier`, `Scout`, `Nurse`) and for predators; `CasteModifiers.Range` scales the radius of `Frontier` and `Levy` directions.
- View side: `EntityViewModel`, `EntityView` in the same folder.

## Brain (`Assets/Scripts/EntitySystem/AI/SharedAI/`)

- Two levels. Coordinator picks a `Goal`: `Survive`, `Hunt`, `Forage`, `Social`. Executor of that goal picks a verb from `ActionSubsets` and a direction.
- Verbs (`EntityAction`, 11): `Idle`, `MoveTo`, `Attack`, `Eat`, `Reproduce`, `Signal`, `Rest`, `Give`, `Take`, `Put`, `MoveNest` (`PlanOnly`).
  Subsets: Survive `Idle MoveTo Eat Rest`; Hunt `Idle MoveTo Attack Signal`; Forage `MoveTo Eat Signal Take Put`; Social `Idle MoveTo Reproduce Signal Give`.
- Directions (`DirectionKind`, 14 kinds): `Food`, `NestFood`, `OwnPoi`, `ColonyPoi`, `Frontier`, `Migration`, `Flock`, `Levy`, `Nest`, `Escape`, `Kin`, `Danger`, `Object`, `Task`.
  Every executor outputs a direction choice and an offset; the available kinds are a `ushort` mask (`EntityModel.ComputeDirections`), narrowed per plan by `PlanCatalog.DirectionMask`.
  `Task` is reserved for the colony task board and is never set yet; with `maskTaskSignal` its bit is cleared from the mask after `ComputeDirections` (`EntityModel.Cycle.cs`).
- Exec aux layout per goal: Hunt and Social keep heading, curvature, style, tempo and speech first, then the direction block, then the offset,
  then the speech-context decode bits (`SharedHierarchicalBrain.DirectionAux`, `DirOffsetAux`, `ExecAux`).
- Genetic predisposition: `GeneticParameters.Predisposition0..2` (`SimulationRules.PredispositionSize`, range [-1, 1]) are phenotypic genes:
  random for a founder (`Default`), mean of both parents plus the usual mutation in `Crossover`, mutation of the copy for a clonal child;
  `EvolveColony` does not overwrite them.
  `EntityModel` copies them into a per-agent array that `VectorizerFrame.Predisposition` passes to `InputVectorizer`,
  which writes them at `PredispositionOffset` (the last `PredispositionSize` inputs) for every agent.
  Input size is 129.
- Each level is an LSTM plus a `DelayedPerceptron` (`Assets/Scripts/EntitySystem/AI/LSTM/`, `Assets/Scripts/EntitySystem/AI/DelayedPerceptron/`).
- Per-agent state: `EntityBrainContext`, `LSTMContext`, `PerceptronContext`; buffers from `ContextSlabs` and `FloatSlabPool`.
- Batched inference: `BeginDecisionBatch` → `EnqueueDecision` → `RunDecisions`; kernels in `NeuralKernels.cs`, jobs in `NeuralJobs.cs`.
- Delayed credit: `DecisionRing` (preallocated ring of pending decisions).
- Learning: truncated BPTT, reward-weighted; gradients collected in `SharedGradientAccumulator`, applied by `ApplyPendingGradients`.
- `ValueCritic`: linear TD critic. `RandomNetworkDistillation`: novelty reward. `RunningMeanStd`: normalisation.
- Genome: `GeneticParameters` (learning rate, lambda, temperature and others), `MimicPhenotype`.
- Plans: `PlanCatalog` (`PlanKind`: `Graze`, `Harvest`, `Patrol`, `Hunt`, `Rest`, `Flee`, `Court`, `Migrate`, `Seek`; steps are a verb plus `PlanProgress.Direction`) and `PlanRunner`
  in `Assets/Scripts/EntitySystem/AI/Plans/`.

## Saving

- `SharedBrainSnapshot` v13 records the executor profile and reads v12 as standard. Wide Social uses a separate model filename suffix.
- An unknown version, truncated data or another architecture is refused with an error; the colony keeps a new brain.
- Model loaders check sizes against the expected architecture before allocating and reject non-finite weights.

## Perception (`Assets/Scripts/EntitySystem/AI/Perception/`)

- `EntitySpatialGrid`: counting-sort buckets of agents; `FindNearest`, `FindInRadius`, `Survey` (the reference for `SurveySystem`, not called in the tick).
- `Survey` and `SurveySystem` keep the foreign creature with the lowest score `distance × huntStickMul (last victim) × lerp(huntLowHpMul, 1, Hp fraction) × familiarity`; they also count crowd and kin.
- The grid SoA (colony, alive, HP fraction, cell) is filled in `Rebuild`; `EntityModel.GridSlot` is its slot; death and damage write it in O(1) (`NoteDeath`, `NoteHp`).
- `ChunkFoodIndex`: food, corpses, payloads and taboo marks per chunk; `TryFindNearestFood`, `TryClaim`, `BiteMeat`.
- `TerrainSensor`, `TerrainSample`, `TerrainSteering`: terrain rings around an agent and the steering bias from them.
- `ObjectSensor`: nearest pickable world object (`InventoryItemInfo != null`, not food) within `ObjectSenseRadius` cells through `IWorldCommandTarget.TryGetObject`.
- `WarmthField`: scalar field on hashed cells. `PlayerPresence`: players as seen by agents (`TryFindNearest`, `ReportHit`).

## Per-agent Burst systems

| System | File | Output |
|---|---|---|
| `SurveySystem` | `Assets/Scripts/EntitySystem/AI/Perception/SurveySystem.cs` | Nearest, nearest foreign (hunt score), crowd and kin from the grid SoA; one job per tick batch |
| `InstinctSystem` | `Assets/Scripts/EntitySystem/AI/Instinct/InstinctSystem.cs` | Urges and reflexes (verb plus direction); decides whether the brain is asked |
| `UtilityPriorSystem` | `Assets/Scripts/EntitySystem/AI/Utility/UtilityPriorSystem.cs` | Utility prior for the action choice |
| `BoidSystem` | `Assets/Scripts/EntitySystem/AI/Flock/BoidSystem.cs` | Flocking vector per agent |
| `SpeechBus` | `Assets/Scripts/EntitySystem/AI/Speech/SpeechBus.cs` | Delivery of utterances to listeners |

The `Feeding` latch of `InstinctSystem` keeps the `Eat` reflex until the stomach reaches `SatedFill`.
An urgent creature moves to the front of the batch only inside the same window.
All are indexed by `EntityModel.ManagerIndex` and expose `RemoveSwapBack`. The registry is `docs/OPTIMIZATION.md` section 14.

## Speech (`Assets/Scripts/EntitySystem/AI/Speech/`)

- `Utterance`: a short token sequence. `SpeechLexicon`: token labels and encoding. `SpeechMetrics`: entropy, mutual information, decode accuracy.
- `SpeechVoiceBank`: Burst synthesis of syllables. `SpeechAudioPlayer`: plays audible utterances. `SpeechLabelFeed`: text labels.
- Players speak through the same bus (`PlayerController.SpeakServerRpc`).

## Other

- Fire is a map event: `docs/map/map-events.md`.
  `EntityManager` sets `GlobalMapEvents.Season` to `SeasonClockSource` and, once per fire step (`ApplyFireHeat`),
  applies `FireEvent.HeatDamageAt` to agents and calls `EntityModel.ReportHazard` for agents within `fireSenseRadius` of a burning cell (threat source, stress, negative memory mark,
  colony `Danger` mark).
- Wild winds are a map event: `docs/map/map-events.md`.
  `EntityManager.ApplyWildWind` runs once per wind step while `WildWindEvent.Active`: an agent in the band reports itself and takes `DamageAt` with `DeathCause.Wind`.
- Laws that change rule axes at run time (codex): `docs/map/codex.md`; `EnergyFlows.Codex` is the outflow of spent votes.
- Rules are partial files `SimulationRules.<Area>.cs` in `Assets/Scripts/EntitySystem/AI/Rules/`; the asset is `Assets/Scripts/EntitySystem/AI/SimulationRules.asset`.
- `RulesFrame` is generated by `tools/rulesframe.py` into `SimulationRules.Frame.g.cs` from the `[FrameField]` fields (`FrameFieldAttribute`; an optional argument is the frame name).
- Frame members that transform a value (clamp, `Fill*`, tables) are assigned in `FrameDerived` in `SimulationRules.Frame.cs`, which also holds the hand-written `RulesFrame` methods.

## Invariants

- Agents tick only from `EntityManager.Update`; there is no second tick path.
  An urgent tick (damage) is a reorder of the current snapshot by `PromoteUrgent`, not a second path: the batch size stays `TickWindowBudget.Take`.
- A cycle has two phases around the survey: `EntityModel.BeginCycle` (before) and `ContinueCycle` (after); `RunTickBatch` runs the survey job between the two loops.
- Simulation time is `SimulationClock`; Unity time is read only where `SimulationClock.Advance` is called.
- A change of `InputVectorizer.VectorSize`, `PlanCatalog.Count`, `ActionCatalog.Count` or layer sizes increments `SharedBrainSnapshot.Version`.
- Access to a colony is explicit (`EntityManager.Colonies`, `ColonyRegistry.TryGetById`, `EntityModel.Colony`); nests are not in the `ServiceLocator`.
- Removal of an agent is swap-back in one place: `ColonyPopulation.RemoveEntitySwapBack`.
- New numeric constants go into `SimulationRules`; a field the tick reads gets `[FrameField]`, then `python3 tools/rulesframe.py gen`.
- Agent code never reads `ViewOrientation`.

## Open issues

None recorded here. Security findings: `docs/audit/security_audit.md`.
