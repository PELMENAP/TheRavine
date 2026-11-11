# AI: agents, colonies, shared brain

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Rules for this folder: `Assets/Scripts/EntitySystem/AI/CLAUDE.md`.
Tests: `Assets/Tests/EditMode/` — `BrainAllocationTests`, `BrainLearningTests`, `BrainArchitectureVersionTests`, `SnapshotVersionTests`,
`SoaSwapBackTests`, `TickWindowBudgetTests`, `DecisionRingTests`, `ColonyRegistryTests`, `EntitySpatialGridSurveyTests`, `RulesDefaultsTests`.

Tick order and hot methods: `docs/map/HOT_PATHS.md`. Existing structures to reuse: `docs/OPTIMIZATION.md` section 13.
Large files (`EntityManager.cs`, `SharedHierarchicalBrain.cs`, `ChunkFoodIndex.cs`, `SimulationRules.cs`, `EntityModel.cs`): `docs/map/LARGE_FILES.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `EntityManager` | `Assets/Scripts/EntitySystem/AI/EntityManager.cs` | MonoBehaviour: lifecycle, tick driver, colonies, visibility, brain saving |
| `EntityModel` | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.cs` | One agent; partial files below |
| `SharedHierarchicalBrain` | `Assets/Scripts/EntitySystem/AI/SharedAI/SharedHierarchicalBrain.cs` | One brain per colony, weights shared by its agents |
| `SimulationRules` | `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.cs` | All simulation constants (ScriptableObject); `Frame` snapshot for the tick |
| `ColonyRegistry` | `Assets/Scripts/EntitySystem/AI/Colony/ColonyRegistry.cs` | Colonies by index and id; `Views` for jobs |
| `ColonyPopulation` | `Assets/Scripts/EntitySystem/AI/Population/ColonyPopulation.cs` | Spawn, death, reproduction, swap-back removal |
| `ActionCatalog` | `Assets/Scripts/EntitySystem/AI/ActionCatalog.cs` | Action list, flags, goal and caste masks; enum in `EntityAction.cs` |
| `InputVectorizer` | `Assets/Scripts/EntitySystem/AI/InputVectorizer.cs` | Observation → input vector (`VectorSize`) |
| `TickWindowBudget` | `Assets/Scripts/EntitySystem/AI/TickWindowBudget.cs` | How many agents tick this frame |

## Agent (`Assets/Scripts/EntitySystem/AI/Entity/`)

- `EntityModel` partial files: `.Cycle` (tick), `.Behaviour`, `.Social`, `.Directions`, `.Homeostasis`, `.Wild`, `.Fitness`.
- Components in `Assets/Scripts/EntitySystem/AI/Entity/Components/`: `BrainComponent`, `DigestionComponent`, `FitnessComponent`,
  `MortalityComponent`, `PerceptionComponent`, `PointsOfInterestComponent`, `SpeechComponent`, `StatsComponent`, `VisualCullingComponent`.
- Commands, one file each, in `Assets/Scripts/EntitySystem/AI/Entity/Commands/`; they run through `EntityCommandRunner`.
- `CreatureCore`: metabolism, vitals and virology step shared by agents and the player.
- `EntityTuning`: per-agent multipliers by `Caste` (`Worker`, `Soldier`, `Scout`, `Nurse`) and for predators.
- View side: `EntityViewModel`, `EntityView` in the same folder.

## Brain (`Assets/Scripts/EntitySystem/AI/SharedAI/`)

- Two levels. Coordinator picks a `Goal`: `Survive`, `Hunt`, `Forage`, `Social`. Executor of that goal picks an action from `ActionSubsets`.
- Each level is an LSTM plus a `DelayedPerceptron` (`Assets/Scripts/EntitySystem/AI/LSTM/`, `Assets/Scripts/EntitySystem/AI/DelayedPerceptron/`).
- Per-agent state: `EntityBrainContext`, `LSTMContext`, `PerceptronContext`; buffers from `ContextSlabs` and `FloatSlabPool`.
- Batched inference: `BeginDecisionBatch` → `EnqueueDecision` → `RunDecisions`; kernels in `NeuralKernels.cs`, jobs in `NeuralJobs.cs`.
- Delayed credit: `DecisionRing` (preallocated ring of pending decisions).
- Learning: truncated BPTT, reward-weighted; gradients collected in `SharedGradientAccumulator`, applied by `ApplyPendingGradients`.
- `ValueCritic`: linear TD critic. `RandomNetworkDistillation`: novelty reward. `RunningMeanStd`: normalisation.
- Genome: `GeneticParameters` (learning rate, lambda, temperature and others), `MimicPhenotype`.
- Plans: `PlanCatalog` (`PlanKind`: `Graze`, `Harvest`, `Patrol`, `Hunt`, `Rest`, `Flee`, `Court`, `Migrate`, `Seek`) and `PlanRunner`
  in `Assets/Scripts/EntitySystem/AI/Plans/`.

## Saving

- `SharedBrainSnapshot` holds `Version`; one file per colony, named by `NeuralModelStorage.ColonyFileName`.
- An unknown version, truncated data or another architecture is refused with an error; the colony keeps a new brain.
- Model loaders check sizes against the expected architecture before allocating and reject non-finite weights.

## Colony (`Assets/Scripts/EntitySystem/AI/Colony/` and the Nest, Caste, Population folders next to it)

- `ColonyState`: `Index`, `ColonyId`, `Brain`, `Nest`, `Pois`, `Stats`, members, leader, nest-site quorum.
- `NestState` (`Assets/Scripts/EntitySystem/AI/Nest/NestState.cs`): channel fields in `NativeArray`, read through `ColonyFieldView`.
- Channels (`ColonyChannel`): `Food`, `Danger`, `Trail`, `KinDeath`, `Explored`, `Sickness`, `Territory`.
- `ColonyPoiBoard`: shared points of interest. `ColonyStats`: counters per colony. `EnergyLedger`: energy flows of the world.
- `SeasonClock`: season value and winter test from `SimulationRules.Frame`.
- `CasteThresholdSystem` (`Assets/Scripts/EntitySystem/AI/Caste/CasteThresholdSystem.cs`): per-agent caste thresholds, SoA.
- `ColonyFood`: food regrowth and corpses. `EvolutionService`: fitness statistics, partner selection, crossover.
- `InfectionCoordinator`: wrapper over `InfectionService`; see `docs/map/virology.md`.

## Perception (`Assets/Scripts/EntitySystem/AI/Perception/`)

- `EntitySpatialGrid`: counting-sort buckets of agents; `Survey`, `FindNearest`, `FindInRadius`.
- `ChunkFoodIndex`: food, corpses, payloads and taboo marks per chunk; `TryFindNearestFood`, `TryClaim`, `BiteMeat`.
- `TerrainSensor`, `TerrainSample`, `TerrainSteering`: terrain rings around an agent and the steering bias from them.
- `WarmthField`: scalar field on hashed cells. `PlayerPresence`: players as seen by agents (`TryFindNearest`, `ReportHit`).

## Per-agent Burst systems

| System | File | Output |
|---|---|---|
| `InstinctSystem` | `Assets/Scripts/EntitySystem/AI/Instinct/InstinctSystem.cs` | Urges and forced actions; decides whether the brain is asked |
| `UtilityPriorSystem` | `Assets/Scripts/EntitySystem/AI/Utility/UtilityPriorSystem.cs` | Utility prior for the action choice |
| `BoidSystem` | `Assets/Scripts/EntitySystem/AI/Flock/BoidSystem.cs` | Flocking vector per agent |
| `SpeechBus` | `Assets/Scripts/EntitySystem/AI/Speech/SpeechBus.cs` | Delivery of utterances to listeners |

All are indexed by `EntityModel.ManagerIndex` and expose `RemoveSwapBack`. The registry is `docs/OPTIMIZATION.md` section 14.

## Speech (`Assets/Scripts/EntitySystem/AI/Speech/`)

- `Utterance`: a short token sequence. `SpeechLexicon`: token labels and encoding. `SpeechMetrics`: entropy, mutual information, decode accuracy.
- `SpeechVoiceBank`: Burst synthesis of syllables. `SpeechAudioPlayer`: plays audible utterances. `SpeechLabelFeed`: text labels.
- Players speak through the same bus (`PlayerController.SpeakServerRpc`).

## Other

- `FireTickHandler` (`Assets/Scripts/EntitySystem/AI/Hazards/FireTickHandler.cs`): fire as a random-tick handler; stepped from `EntityManager.Update`.
- `FireView` (`Assets/Scripts/EntitySystem/AI/Hazards/FireView.cs`): fire rendering, created by `EntityManager` next to `FireTickHandler`; only where the simulation runs (no replication).
- `FireView` rebuilds one `GraphicsBuffer` when `FireTickHandler.Version` changes: burning cells (`Burning`) plus up to 1024 residues of burnt-out or quenched cells.
- `FireView` draws with two mesh-less `Graphics.RenderPrimitives` calls; shaders: `Assets/Resources/Fire/FireSmoke.shader` (scorch, smoke), `Assets/Resources/Fire/FireFlame.shader` (glow, tongues, embers).
- Fire shaders share `Assets/Shaders/Effects/FireCommon.hlsl`; they extrapolate time left from `FireTickHandler.StepTime`, so a fire fades out without a pop.
- `FireView` materials are created from the shaders at runtime unless `EntityManager.fireFlameMaterial` / `fireSmokeMaterial` are set; the look is in material properties.
- `FireView` lights: up to `fireLights` shadowless point lights on the heaviest fire clusters near the camera.
- Debug, off the hot path: `SimulationDebugDashboard`, `SimulationLog`, `NestDebugView` in `Assets/Scripts/EntitySystem/AI/Debug/`.
- Log columns: `docs/simulation_log.md`. Action and property audits: `docs/audit/action_census.md`, `docs/audit/property_system_matrix.md`.
- Rules are partial files `SimulationRules.<Area>.cs` in `Assets/Scripts/EntitySystem/AI/Rules/`; the asset is `Assets/Scripts/EntitySystem/AI/SimulationRules.asset`.

## Invariants

- Agents tick only from `EntityManager.Update`; there is no second tick path.
- Simulation time is `SimulationClock`; Unity time is read only where `SimulationClock.Advance` is called.
- A change of `InputVectorizer.VectorSize`, `PlanCatalog.Count`, `ActionCatalog.Count` or layer sizes increments `SharedBrainSnapshot.Version`.
- Access to a colony is explicit (`EntityManager.Colonies`, `ColonyRegistry.TryGetById`, `EntityModel.Colony`); nests are not in the `ServiceLocator`.
- Removal of an agent is swap-back in one place: `ColonyPopulation.RemoveEntitySwapBack`.
- New numeric constants go into `SimulationRules`, plus a `RulesFrame` field if read in the tick.
- Agent code never reads `ViewOrientation`.

## Open issues

None recorded here. Security findings: `docs/audit/security_audit.md`.
