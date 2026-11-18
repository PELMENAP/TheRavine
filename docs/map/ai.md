# AI: agents, colonies, shared brain

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Rules for this folder: `Assets/Scripts/EntitySystem/AI/CLAUDE.md`.
Tests: `Assets/Tests/EditMode/` — `BrainAllocationTests`, `BrainLearningTests`, `BrainArchitectureVersionTests`, `SnapshotVersionTests`,
`SoaSwapBackTests`, `TickWindowBudgetTests`, `DecisionRingTests`, `ColonyRegistryTests`, `EntitySpatialGridSurveyTests`, `SurveySystemTests`, `AgentSnapshotCodecTests`, `RulesDefaultsTests`,
`ExperimentArmTests`, `LtcNeuronTests`, `NoveltyProgressTests`, `PredispositionTests`, `ReservoirMemoryCapacityTests`, `NestSiteScoreTests`, `FamiliarityTests`, `CuriositySurplusTests`, `StartleTests`, `FoodLookupAllocationTests`, `PredatorPressureTests`, `SkillGraphTests`, `SkillGraphAgentTests`.

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
| `UrgentTickQueue` | `Assets/Scripts/EntitySystem/AI/UrgentTickQueue.cs` | Creatures that took damage and are due for an early tick; `EntityManager.PromoteUrgent` moves them to the front of the current batch inside the same window |

## Agent (`Assets/Scripts/EntitySystem/AI/Entity/`)

- `EntityModel` partial files: `.Cycle` (tick), `.Behaviour`, `.Social`, `.Directions`, `.Homeostasis`, `.Wild`, `.Fitness`, `.Skills`.
- Components in `Assets/Scripts/EntitySystem/AI/Entity/Components/`: `BrainComponent`, `DigestionComponent`, `FitnessComponent`,
  `MortalityComponent`, `PerceptionComponent`, `PointsOfInterestComponent`, `SpeechComponent`, `StatsComponent`, `VisualCullingComponent`.
- Commands, one file each, in `Assets/Scripts/EntitySystem/AI/Entity/Commands/`; they run through `EntityCommandRunner`.
- One command per verb. `MoveToCommand` picks its behaviour from the decision's `DirectionKind`; `SignalCommand` from the goal of the decision; `GiveCommand` from the receiver; `TakeCommand` and `PutCommand` from the direction and the carried load.
- Held object: `EntityModel.HeldObject` and `HeldAmount` (`EntityModel.Objects.cs`). `Take` with direction `Object` does not edit the world, so `HeldObject` stays empty; the object direction and proximity inputs are filled from `ObjectSensor`.
- `CreatureCore`: metabolism, vitals and virology step shared by agents and the player.
- `EntityTuning`: per-agent multipliers by `Caste` (`Worker`, `Soldier`, `Scout`, `Nurse`) and for predators; `CasteModifiers.Range` scales the radius of `Frontier` and `Levy` directions.
- View side: `EntityViewModel`, `EntityView` in the same folder.

## Brain (`Assets/Scripts/EntitySystem/AI/SharedAI/`)

- Two levels. Coordinator picks a `Goal`: `Survive`, `Hunt`, `Forage`, `Social`. Executor of that goal picks a verb from `ActionSubsets` and a direction.
- Verbs (`EntityAction`, 11): `Idle`, `MoveTo`, `Attack`, `Eat`, `Reproduce`, `Signal`, `Rest`, `Give`, `Take`, `Put`, `MoveNest` (`PlanOnly`). Subsets: Survive `Idle MoveTo Eat Rest`; Hunt `Idle MoveTo Attack Signal`; Forage `MoveTo Eat Signal Take Put`; Social `Idle MoveTo Reproduce Signal Give`.
- Directions (`DirectionKind`, 14 kinds): `Food`, `NestFood`, `OwnPoi`, `ColonyPoi`, `Frontier`, `Migration`, `Flock`, `Levy`, `Nest`, `Escape`, `Kin`, `Danger`, `Object`, `Task`. Every executor outputs a direction choice and an offset; the available kinds are a `ushort` mask (`EntityModel.ComputeDirections`), narrowed per plan by `PlanCatalog.DirectionMask`. `Task` is reserved for the colony task board and is never set yet.
- Exec aux layout per goal: Hunt and Social keep heading, curvature, style, tempo and speech first, then the direction block, then the offset, then the speech-context decode bits (`SharedHierarchicalBrain.DirectionAux`, `DirOffsetAux`, `ExecAux`).
- Null policy: when `SharedHierarchicalBrain.NullPolicy` is set (colony arm `NullPolicy`), `RunDecisions` replaces the sampled plan by a uniform draw over `PlanCatalog.IsFeasible` plans and the sampled executor verb by a uniform draw over the verbs of `ctx.ExecMask`; direction, heading and speech tokens still come from the brain's outputs. Both tickets are created with `Trained = true`, so no critic update and no gradient ticket follows, and `_rnd.Collect` is skipped; instincts, plans and rules are untouched.
- Gradient conflict (diagnostic, `SimulationRules.gradientConflictEnabled`, interval `gradientConflictInterval` applications): in a conflict cycle `PerceptronTrainJob` additionally accumulates each executor ticket's scaled gradient into per-thread, per-caste buffers (`TrainTicket.Group` = `PerceptronContext.Group`, set by `EntityBrainContext.SetGroup` in `EntityModel.AssignCaste`; juveniles and the wild colony have group -1 and are skipped). `SharedHierarchicalBrain.ApplyPendingGradients` reduces them in `DelayedPerceptron.AccumulateGroupConflict` into `GradientConflict` (`Cos` of each caste pair over all four executors, `Norm` = norm of the summed gradients divided by the caste's ticket count) before the real apply, which is unchanged. With the flag off nothing is allocated. `SharedGradientAccumulator` is not on this path.
- LTC update mode (`LtcMode`, math in `Assets/Scripts/EntitySystem/AI/SharedAI/LtcNeuron.cs`, called by `NeuralKernels.Layer` and the backward pass): `Legacy` is `h' = (h + dt·f)/(1 + dt/tau)`; `Bounded` (arm `BoundedLtc`) is `z = dt/(tau + dt)`, `h' = (1 − z)·h + z·f`, whose state stays in [-1, 1], with the exact backward pass (no `tau` gradient where `tau` is clamped). `tau` is clamped to `[ltcTauMin, ltcTauMax]` (defaults `TauEpsilon` and `float.MaxValue`, so `Legacy` is unchanged). The mode and clamp live in `KernelLayout` (`LtcBounded`, `TauMin`, `TauMax`) per `DelayedPerceptron`, set by `SetLtc`.
- Novelty mode: `Error` is the raw `RandomNetworkDistillation` error; the arm `ProgressNovelty` replaces it in `SharedHierarchicalBrain.ReadIntrinsic` by `max(0, slow − fast)` of two EMAs of the error per agent (`NoveltyProgressSystem`, SoA by `ManagerIndex`, rates `noveltyFastRate`, `noveltySlowRate`; `EntityBrainContext.NoveltyIndex` is set in `EntityModel.SubmitDecision`). The system is registered in the `ServiceLocator` by `EntityManager`.
- Reservoir init: the arm `OrthogonalReservoir` replaces the recurrent block of each gate of a newly created `LSTMMemory` by a random orthogonal matrix scaled to `reservoirSpectralRadius` (`LSTMMemory.OrthogonalizeRecurrent`); a brain loaded from a file keeps its saved reservoir.
- Genetic predisposition: `GeneticParameters.Predisposition0..2` (`SimulationRules.PredispositionSize`, range [-1, 1]) are phenotypic genes: random for a founder (`Default`), mean of both parents plus the usual mutation in `Crossover`, mutation of the copy for a clonal child; `EvolveColony` does not overwrite them. `EntityModel` copies them into a per-agent array that `VectorizerFrame.Predisposition` passes to `InputVectorizer`, which writes them at `PredispositionOffset` (the last `PredispositionSize` inputs) for every agent. Input size is 129, `SharedBrainSnapshot.Version` is 12.
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

- `SharedBrainSnapshot` holds `Version`; one file per colony, named by `NeuralModelStorage.ColonyFileName`.
- An unknown version, truncated data or another architecture is refused with an error; the colony keeps a new brain.
- Model loaders check sizes against the expected architecture before allocating and reject non-finite weights.

## Colony (`Assets/Scripts/EntitySystem/AI/Colony/` and the Nest, Caste, Population folders next to it)

- `ColonyState`: `Index`, `ColonyId`, `Arm`, `Brain`, `Nest`, `Pois`, `Stats`, members, leader, nest-site quorum. A nest-site vote is weighted by `ColonyState.SiteScore` (slope, relative height, food field, distance from the nest).
- Experiment arm (`ColonyArm` in `Assets/Scripts/EntitySystem/AI/Colony/ColonyArm.cs`): `Control`, `SpeechMuted`, `SpeechShuffled`, `NullPolicy`, `BoundedLtc`, `ProgressNovelty`, `OrthogonalReservoir`. `ColonyState` draws it once in its constructor through `ColonyArms.Draw` from `SimulationRules.arm<Name>Fraction` (`SimulationRules.ArmFraction`; all 0: every colony is `Control`); the wild colony is always `Control`. Colony state is not saved, so the arm is not either; `ColonyArms.FromByte` maps an unknown value to `Control`. The arm reaches the speech bus through `SpeechBus.Reset(..., arm)` per agent and the brain through `SharedHierarchicalBrain.SetArm` (called by `ColonyState` for a new brain and by `ReplaceBrain`); the log column is `arm`. The older per-agent `speechMuteSampling` (listener deafness) is separate and unchanged.
- `ColonyState.Pressure`: 0..1, raised by damage from a foreign attacker and by `Killed` deaths, decays with `pressureTau`. It raises the soldier stimulus in `EntityManager.ChooseCaste`, lowers the vote weight the quorum needs (`pressureQuorumRelief`), adds to the nest danger term of migration and to the `Migrate` plan bias.
- Wild colony: `ColonyState.PackCentre` and `PackCount` are refreshed once per window by `EntityManager.UpdatePack`; the instinct `InstinctBits.Pack` pulls a creature farther than `packRadius` back toward the pack (`DirectionKind.Kin`); predators spawn within `predatorPackSpread` of a pack member; boid weights come from `packBoid*`. `CasteModifiers.Fear` (`predatorFearMul`) scales outnumbered, startle and foreign-harm stress; `CasteModifiers.HuntBias` (`predatorHuntBias`) biases the `Hunt` plan.
- `NestState` (`Assets/Scripts/EntitySystem/AI/Nest/NestState.cs`): channel fields in `NativeArray`, read through `ColonyFieldView`.
- Channels (`ColonyChannel`): `Food`, `Danger`, `Trail`, `KinDeath`, `Explored`, `Sickness`, `Territory`.
- `ColonyPoiBoard`: shared points of interest; `Offer` merges, `ReportEmpty` lowers `EnergyEma` of a point within the merge radius and never creates one. `ColonyStats`: counters per colony. `EnergyLedger`: energy flows of the world.
- `SeasonClock`: season value and winter test from `SimulationRules.Frame`.
- `CasteThresholdSystem` (`Assets/Scripts/EntitySystem/AI/Caste/CasteThresholdSystem.cs`): per-agent caste thresholds, SoA.
- `ColonyFood`: food regrowth and corpses. `EvolutionService`: fitness statistics, partner selection, crossover.
- `InfectionCoordinator`: wrapper over `InfectionService`; see `docs/map/virology.md`.

## Skills (`Assets/Scripts/EntitySystem/AI/Skills/`)

Design: `docs/design/skill_graph.md`. Rules: `SimulationRules.Skills.cs`; `RulesFrame` mirrors the fields the math reads.

- `SkillAtlas`: ScriptableObject, one per world; points, `PotentialRow` adjacency masks, per-skill curve and XP cost, XP table by `EntityAction`. `SkillAtlas.Active` is bound by `SkillAtlas.Bind`; unbound it is `SkillAtlasBuilder.BuildDefault()` with a fixed seed, so every machine builds the same atlas.
- `SkillAtlasBuilder`: Poisson points, Bowyer–Watson Delaunay, second diagonals by cocircularity, K5 check (`HasK5`). The editor menu `Tools/Ravine/Skills/Build Atlas` is `Assets/Scripts/Editor/SkillAtlasMenu.cs`.
- `SkillGraphTopology` (+ `.Tarjan`): `Active` rows, `IslandMask`, `PrunedMask`, budget; `Create`, `TryBuild`, `TryPrune`, `TryRestore`; one iterative Tarjan pass rebuilds bridges, `CliqueSize`, `CycleFactor`, `ComponentOf`.
- `SkillGraphMath`: static rules 1–12 of the design, Burst-compatible, rules by `in RulesFrame`.
- `SkillGraphStore`: per-agent SoA (`Levels`, `Peaks`, `LastUsed`, `Talents`, `Roots`, `Dirty`); `Reset`, `Retarget`, `AddXp`, `Materialize`, `RemoveSwapBack`.
- `SkillGraphQuery.Fill` writes a `SkillGraphSnapshot` for a view through `ISkillGraphSource`; `ISkillGraphCommands` is the command side (`PlayerSkillGraph` implements both). View: `docs/map/inventory-ui.md`.
- `SkillModifiers`: one multiplier per `SkillChannel`; `From(store, index, topology, atlas)`.
- `SkillGraphXp.Award` adds the XP of a completed `EntityAction`; `EntityCommandRunner.Finish` calls it through `EntityModel.AwardSkillXp` for every creature.
- `EntityModel.RefreshSkillTuning` re-expresses tuning at the start of a cycle when the store `Dirty` flag is set; `EntityTuning.Express` with `SkillModifiers` and `CasteModifiers.Combine`.
- `SkillDecayJob`: Burst sweep over a sliding window of agents from `EntityManager.SweepSkills`; reads per-colony cycle factors published by `SkillGraphStore.PublishColony`.
- `SkillGraphSolver.Step`: per-colony plasticity (build, prune) once per `SkillSolverInterval` from `EntityManager.SolveSkills`; `ColonyState.SkillTopology` is the colony graph, the wild colony owns one too.
- Skill genes (`SkillOriginX/Y`, `Plast*`) are phenotypic: `CopyLearningGenes` never overwrites them. Saved brains do not hold genes.
- `SkillAtlas.Skill` is a graph vertex; the ability skills of `Assets/Scripts/Model/EntitySkills/` (`ISkill`) are unrelated.

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
| `InstinctSystem` | `Assets/Scripts/EntitySystem/AI/Instinct/InstinctSystem.cs` | Urges and reflexes (verb plus direction); decides whether the brain is asked. The `Feeding` latch keeps the `Eat` reflex until the stomach reaches `SatedFill` |
| `UtilityPriorSystem` | `Assets/Scripts/EntitySystem/AI/Utility/UtilityPriorSystem.cs` | Utility prior for the action choice |
| `BoidSystem` | `Assets/Scripts/EntitySystem/AI/Flock/BoidSystem.cs` | Flocking vector per agent |
| `SpeechBus` | `Assets/Scripts/EntitySystem/AI/Speech/SpeechBus.cs` | Delivery of utterances to listeners |

All are indexed by `EntityModel.ManagerIndex` and expose `RemoveSwapBack`. The registry is `docs/OPTIMIZATION.md` section 14.

## Speech (`Assets/Scripts/EntitySystem/AI/Speech/`)

- `Utterance`: a short token sequence. `SpeechLexicon`: token labels and encoding. `SpeechMetrics`: entropy, mutual information, decode accuracy.
- `SpeechVoiceBank`: Burst synthesis of syllables. `SpeechAudioPlayer`: plays audible utterances. `SpeechLabelFeed`: text labels.
- Players speak through the same bus (`PlayerController.SpeakServerRpc`).
- Arms in the bus: `SpeechBus.ApplyArm` runs on the main thread over the batch before the credit pass and the deliver job. A `SpeechMuted` speaker's utterance gets `Len = 0` (nothing is delivered or made audible; the speaker still pays and emits). A `SpeechShuffled` speaker's utterance keeps time, loudness, length, position and speaker; each token slot is replaced by the same slot of a random entry of the colony's ring (`SimulationRules.SpeechShuffleRing`, fixed arrays by colony index) and the context byte by the context of another random entry; an empty ring gives a uniform token. The ring stores the true tokens, so it follows the colony's own distribution. Listeners of other colonies get the changed utterance.
- `ColonySpeechTables` (`Assets/Scripts/EntitySystem/AI/Speech/ColonySpeechTables.cs`, one per colony inside `SpeechBus`, cleared by `SimulationLog` after each sample): context×token count table of the speakers, filled in `ApplyArm` from the true utterance (reflex cries excluded), and fresh/no-token × action table of the listeners, filled by `EntityModel.NoteCommandStart` from `EntityCommandRunner.Start`. It gives `speech_mi_bits` and `listener_shift` of the log.

## Other

- Fire is a map event: `docs/map/map-events.md`. `EntityManager` sets `GlobalMapEvents.Season` to `SeasonClockSource` and, once per fire step (`ApplyFireHeat`), applies `FireEvent.HeatDamageAt` to agents and calls `EntityModel.ReportHazard` for agents within `fireSenseRadius` of a burning cell (threat source, stress, negative memory mark, colony `Danger` mark).
- Wild winds are a map event: `docs/map/map-events.md`. `EntityManager.ApplyWildWind` runs once per wind step while `WildWindEvent.Active`: an agent in the band reports itself and takes `DamageAt` with `DeathCause.Wind`.
- Debug, off the hot path: `SimulationDebugDashboard`, `SimulationLog`, `BehaviourDescriptorLog`, `NestDebugView` in `Assets/Scripts/EntitySystem/AI/Debug/`.
- `SimulationLog` writes `h_saturated_<arm>` (share of hidden states with `|h| > 0.95` over the coordinator and executor contexts of the agents of each arm, via `PerceptronContext.CountSaturated`) and `predisposition_dist` (`EvolutionService.MeanPredispositionDistance`).
- `BehaviourDescriptorLog` (flag `behaviourDescriptorsEnabled`, needs `simulationLogEnabled`): created by `EntityManager` only when both are on and registered in the `ServiceLocator`; per-agent window counters in SoA `NativeArray` by `ManagerIndex` (actions started, goal at start, path length from `RunCycleTail`, utterances, strikes, foods eaten), fed from `EntityModel.NoteCommandStart`, `Emit`, `RegisterFitnessEvent`; `SimulationLog.Sample` calls `Sample`, which writes one row per living agent to `behaviour_descriptors_<same stamp>.csv` and clears the counters. Analysis: `tools/simlog_analyze.py`.
- Log columns: `docs/simulation_log.md`. Action and property audits: `docs/audit/action_census.md`, `docs/audit/property_system_matrix.md`.
- Laws that change rule axes at run time (codex): `docs/map/codex.md`; `EnergyFlows.Codex` is the outflow of spent votes.
- Rules are partial files `SimulationRules.<Area>.cs` in `Assets/Scripts/EntitySystem/AI/Rules/`; the asset is `Assets/Scripts/EntitySystem/AI/SimulationRules.asset`.

## Invariants

- Agents tick only from `EntityManager.Update`; there is no second tick path. An urgent tick (damage) is a reorder of the current snapshot by `PromoteUrgent`, not a second path: the batch size stays `TickWindowBudget.Take`.
- A cycle has two phases around the survey: `EntityModel.BeginCycle` (before) and `ContinueCycle` (after); `RunTickBatch` runs the survey job between the two loops.
- Simulation time is `SimulationClock`; Unity time is read only where `SimulationClock.Advance` is called.
- A change of `InputVectorizer.VectorSize`, `PlanCatalog.Count`, `ActionCatalog.Count` or layer sizes increments `SharedBrainSnapshot.Version`.
- Access to a colony is explicit (`EntityManager.Colonies`, `ColonyRegistry.TryGetById`, `EntityModel.Colony`); nests are not in the `ServiceLocator`.
- Removal of an agent is swap-back in one place: `ColonyPopulation.RemoveEntitySwapBack`.
- New numeric constants go into `SimulationRules`, plus a `RulesFrame` field if read in the tick.
- Agent code never reads `ViewOrientation`.

## Open issues

None recorded here. Security findings: `docs/audit/security_audit.md`.
