# AI experiments: arms, ablations, instrumentation

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Agent and colony map: `docs/map/ai.md`.
Tests: `Assets/Tests/EditMode/` — `ExperimentArmTests`, `ExperimentScenarioTests`, `AnomalyDetectorTests`, `LtcNeuronTests`, `NoveltyProgressTests`, `ReservoirMemoryCapacityTests`.
Log columns: `docs/simulation_log.md`. Analysis: `tools/simlog_analyze.py`.

## Brain modes and arms

- Null policy: when `SharedHierarchicalBrain.NullPolicy` is set (colony arm `NullPolicy`),
  `RunDecisions` replaces the sampled plan by a uniform draw over `PlanCatalog.IsFeasible` plans and the sampled executor verb by a uniform draw over the verbs of `ctx.ExecMask`;
  direction, heading and speech tokens still come from the brain's outputs.
  Both tickets are created with `Trained = true`, so no critic update and no gradient ticket follows, and `_rnd.Collect` is skipped; instincts, plans and rules are untouched.
- Gradient conflict (diagnostic, `SimulationRules.gradientConflictEnabled`, interval `gradientConflictInterval` applications):
  in a conflict cycle `PerceptronTrainJob` additionally accumulates each executor ticket's scaled gradient into per-thread,
  per-caste buffers (`TrainTicket.Group` = `PerceptronContext.Group`, set by `EntityBrainContext.SetGroup` in `EntityModel.AssignCaste`;
  juveniles and the wild colony have group -1 and are skipped).
  `SharedHierarchicalBrain.ApplyPendingGradients` reduces them in `DelayedPerceptron.AccumulateGroupConflict` into `GradientConflict` (`Cos` of each caste pair over all four executors,
  `Norm` = norm of the summed gradients divided by the caste's ticket count) before the real apply, which is unchanged.
  With the flag off nothing is allocated.
  `SharedGradientAccumulator` is not on this path.
- LTC update mode (`LtcMode`, math in `Assets/Scripts/EntitySystem/AI/SharedAI/LtcNeuron.cs`, called by `NeuralKernels.Layer` and the backward pass): `Legacy` is `h' = (h + dt·f)/(1 + dt/tau)`;
  `Bounded` (arm `BoundedLtc`) is `z = dt/(tau + dt)`, `h' = (1 − z)·h + z·f`, whose state stays in [-1, 1], with the exact backward pass (no `tau` gradient where `tau` is clamped).
  `tau` is clamped to `[ltcTauMin, ltcTauMax]` (defaults `TauEpsilon` and `float.MaxValue`, so `Legacy` is unchanged).
  The mode and clamp live in `KernelLayout` (`LtcBounded`, `TauMin`, `TauMax`) per `DelayedPerceptron`, set by `SetLtc`.
- Novelty mode: `Error` is the raw `RandomNetworkDistillation` error;
  the arm `ProgressNovelty` replaces it in `SharedHierarchicalBrain.ReadIntrinsic` by `max(0, slow − fast)` of two EMAs of the error per agent (`NoveltyProgressSystem`,
  SoA by `ManagerIndex`, rates `noveltyFastRate`, `noveltySlowRate`;
  `EntityBrainContext.NoveltyIndex` is set in `EntityModel.SubmitDecision`).
  The system is registered in the `ServiceLocator` by `EntityManager`.
- Reservoir init: the arm `OrthogonalReservoir` replaces the recurrent block of each gate of a newly created `LSTMMemory` by a random orthogonal matrix scaled to `reservoirSpectralRadius`
  (`LSTMMemory.OrthogonalizeRecurrent`);
  a brain loaded from a file keeps its saved reservoir.
- Experiment arm (`ColonyArm` in `Assets/Scripts/EntitySystem/AI/Colony/ColonyArm.cs`):
  `Control`, `SpeechMuted`, `SpeechShuffled`, `NullPolicy`, `BoundedLtc`, `ProgressNovelty`, `OrthogonalReservoir`.
  `ColonyState` draws it once in its constructor through `ColonyArms.Draw` from `SimulationRules.arm<Name>Fraction` (`SimulationRules.ArmFraction`;
  all 0: every colony is `Control`);
  the wild colony is always `Control`.
  Colony state is not saved, so the arm is not either.
  The arm reaches the speech bus through `SpeechBus.Reset(..., arm)` per agent and the brain through `SharedHierarchicalBrain.SetArm` (called by `ColonyState` for a new brain and by `ReplaceBrain`);
  the log column is `arm`.
  The older per-agent `speechMuteSampling` (listener deafness) is separate and unchanged.
- Transplant (`transplantSchedule`, `transplantTrainGuests`): `ColonyPopulation.TickTransplants` from `CloseTickWindow`;
  `EntityModel.GuestOfColonyId`, `ColonyState.GuestCount`, `EntityBrainContext.TrainOff` (read in `RunDecisions` and `EnqueueDecision`), `ColonyStats.Guest*`.
  Details and columns: `docs/simulation_log.md`.

## Speech ablation

- Arms in the bus: `SpeechBus.ApplyArm` runs on the main thread over the batch before the credit pass and the deliver job.
  A `SpeechMuted` speaker's utterance gets `Len = 0` (nothing is delivered or made audible; the speaker still pays and emits).
  A `SpeechShuffled` speaker's utterance keeps time, loudness, length, position and speaker;
  each token slot is replaced by the same slot of a random entry of the colony's ring (`SimulationRules.SpeechShuffleRing`,
  fixed arrays by colony index) and the context byte by the context of another random entry;
  an empty ring gives a uniform token.
  The ring stores the true tokens, so it follows the colony's own distribution.
  Listeners of other colonies get the changed utterance.
- `ColonySpeechTables` (`Assets/Scripts/EntitySystem/AI/Speech/ColonySpeechTables.cs`, one per colony inside `SpeechBus`, cleared by `SimulationLog` after each sample):
  context×token count table of the speakers, filled in `ApplyArm` from the true utterance (reflex cries excluded), and fresh/no-token × action table of the listeners,
  filled by `EntityModel.NoteCommandStart` from `EntityCommandRunner.Start`.
  It gives `speech_mi_bits` and `listener_shift` of the log.

## System flags, masks and the control scenario

- `SimulationRules.Systems.cs`: bools `stressSourcesEnabled`, `socialReliefEnabled`, `helpLogEnabled`, `taskBoardEnabled`, `depotsEnabled`, `trailsEnabled`, `huddlingEnabled`,
  `sheltersEnabled` (all off;
  the systems themselves are added by later stages) and the ablation masks `maskStressInput`, `maskTaskSignal`.
  `RulesFrame` mirrors only the masks.
  `SystemFlagsLabel()` is the flag set written to the run file.
  The same file holds `stressHighThreshold` and the `anomaly*` rules.
- `maskStressInput`: `InputVectorizer.Vectorize` writes 0 into the stress slot (`StateOffset + 12`); input size and layout are unchanged.
  The stress that instincts read (`InstinctInput.Stress`) is not masked.
- `ExperimentScenario` (`Assets/Scripts/EntitySystem/AI/Population/ExperimentScenario.cs`): ScriptableObject assigned to `EntityManager.scenario`.
  Per civil colony: nest offset from the spawn point and a food modifier (`foodMultiplier` inside `foodRadius` of the nest, on in the window `foodStartSeconds`,
  `foodActiveSeconds` on / `foodInactiveSeconds` off, in `SimulationClock` time;
  active 0 or inactive 0 means always on after the start).
  `foundersPerColony` founders are spawned per civil colony.
  Without a scenario `colonyOrigins` and `initialCount` work as before.
  Only initial conditions: no behaviour is scripted.
- `FoodZone` (same file): a modifier built by `ExperimentScenario.BuildZones` and given to `ChunkFoodIndex.SetFoodZones`;
  `TickEcology` multiplies the chunk capacity `K` by the product of the zones that contain the chunk centre and are on.
  Zones stay at the nest positions of the scenario even if a nest relocates.
  Colonies with multiplier 1 build no zone.
- `AnomalyDetector` (`Assets/Scripts/EntitySystem/AI/Debug/AnomalyDetector.cs`): counts transfer loops, relief farming and law-investment concentration; never blocks.
  Created by `EntityManager` only with `simulationLogEnabled`, bound as `AnomalyDetector.Current` (null otherwise), also set as `WorldCodexService.Anomalies`.
  Fed by `GiveCommand` (`NoteTransfer`), `WorldCodexService.TryPropose/TryInvest` (`NoteInvestment`); `NoteRelief(giverId, receiverId, amount)` is the entry point for relief systems.
  `SimulationLog` calls `ConsumeWindow` and `SampleConcentration` once per sample.

## Logs and headless runs

- Headless run: `HeadlessSimulation.Run` (`Assets/Scripts/Editor/HeadlessSimulation.cs`, `-batchmode -nographics -executeMethod`) plays a scene, flushes `SimulationLog` and stores it per replicate;
  wrapper `tools/headless_run.sh`.
- Debug, off the hot path: `SimulationDebugDashboard`, `SimulationLog`, `BehaviourDescriptorLog`, `NestDebugView` in `Assets/Scripts/EntitySystem/AI/Debug/`.
- `SimulationLog` writes `h_saturated_<arm>` (share of hidden states with `|h| > 0.95` over the coordinator and executor contexts of the agents of each arm,
  via `PerceptronContext.CountSaturated`) and `predisposition_dist` (`EvolutionService.MeanPredispositionDistance`).
- `LifeEventLog` (flag `lifeLogEnabled`, independent of `simulationLogEnabled`): created by `EntityManager`, held by `ColonyPopulation.LifeLog`;
  one row per birth (`SpawnEntity`, with parent and mate ids) and per death (`ProcessPendingDeaths`) in `life_log_<stamp>.csv`.
- `VirologyCensus` (`Assets/Scripts/EntitySystem/AI/Debug/VirologyCensus.cs`): strain, lineage and immunity census over the host list at log time;
  event counters come from `VirologyEvents` (`docs/map/virology.md`).
- `BehaviourDescriptorLog` (flag `behaviourDescriptorsEnabled`, needs `simulationLogEnabled`): created by `EntityManager` only when both are on and registered in the `ServiceLocator`;
  per-agent window counters in SoA `NativeArray` by `ManagerIndex` (actions started, goal at start, path length from `RunCycleTail`, utterances, strikes, foods eaten),
  fed from `EntityModel.NoteCommandStart`, `Emit`, `RegisterFitnessEvent`;
  `SimulationLog.Sample` calls `Sample`, which writes one row per living agent to `behaviour_descriptors_<same stamp>.csv` and clears the counters.
  Analysis: `tools/simlog_analyze.py`.
- `ColonySpeechTableLog` (flag `colonySpeechTableEnabled`) and `BrainDistanceLog` (flag `brainDistanceEnabled`, `SharedHierarchicalBrain.ExecutorDot`,
  `ExecutorNormSq`) are created by `SimulationLog` and written in `Sample` before the speech tables are cleared;
  files and analysis: `docs/simulation_log.md`.
- `BehaviourMotifLog` (flag `motifTopK` above 0, needs `simulationLogEnabled`): created by `EntityManager`, registered in the `ServiceLocator`;
  per-agent SoA by `ManagerIndex` (last two actions, buckets, 32-slot motif hash), fed from `EntityModel.NoteCommandStart`, written by `SimulationLog.Sample` to `behaviour_motifs_<stamp>.csv`.
- `ColonyCanvas` (`Assets/Scripts/EntitySystem/AI/Debug/ColonyCanvas.cs`) and `ColonyCanvasView` (IMGUI window, off by default, component next to `SimulationDebugDashboard`):
  observer view of each civil colony, a 100×100 RGBA image rebuilt in `SimulationLog.Sample` only while the view is open or `canvasExportEnabled` is on (`ColonyCanvas.IsActive`);
  nothing runs otherwise.
  Inputs: `ColonySpeechTables` window counts, `ColonyStats` (`ActionsStarted`, `GoalsStarted`, `ActionBigrams`, `GoalPhase`, filled in `EntityModel.NoteCommandStart`) and `NestState` fields.
  Layout and numbers: `docs/simulation_log.md`.
  Nothing in the world reads it.
- Log columns: `docs/simulation_log.md`. Action and property audits: `docs/audit/action_census.md`, `docs/audit/property_system_matrix.md`.
