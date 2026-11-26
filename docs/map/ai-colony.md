# AI: population checkpoints and colonies

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Agent map: `docs/map/ai.md`.
Tests: `Assets/Tests/EditMode/` — `ColonyRegistryTests`, `AgentSnapshotCodecTests`, `NestSiteScoreTests`, `PredatorPressureTests`.

## Population checkpoints

- World authority captures live agents from motion slots and skips pending deaths. Colony IDs identify codex authors and optional brain files.
- Population version 3 stores colony and agent ID high-water marks, experiment arm, learning genome, nest location, food, hunger and leader.
  Version 3 adds each agent's help log; version 2 files load without it and version 1 files also with zero stress.
- Agent records store identity, parent ID, position, simulation age, caste, phenotype, health, energy, stomach, carried food, stress, the loneliness timer,
  the stress level and the help log (partner id, balance, comfort accumulator and the ages of the last events).
- Startup checks scene capacity and gene ranges, prepares chunks, restores colonies and brains, then registers agent slots and resolves leaders and parents.
- Restore uses current phenotype capacities, without birth or initial energy credit. A missing parent keeps its ancestral ID and has no live target.
- Colony fields, membership, observations, plans, migration votes and skill topology are rebuilt. Attack, caste review and election timers resume on the simulation clock.
- Missing population starts founders. An empty checkpoint starts with zero agents. Missing or incompatible neural weights use initialized brains.

## Colony (`Assets/Scripts/EntitySystem/AI/Colony/` and the Nest, Caste, Population folders next to it)

- `ColonyState`: `Index`, `ColonyId`, `Arm`, `Genome`, `Brain`, `Nest`, `Pois`, `Stats`, members, leader, nest-site quorum.
  A nest-site vote is weighted by `ColonyState.SiteScore` (slope, relative height, food field, distance from the nest).
- Extra colonies (`SimulationRules.Culture.cs`: `extraCivilColonies` default 0, `minColonySpacing`, `extraColonyRadius`, `extraColonyCandidates`):
  at a fresh start `EntityManager.CreateColonies` adds them after `colonyOrigins` or the scenario and before the wild colony;
  `ColonySites.Choose` draws candidates around the spawn point, rejects those closer than `minColonySpacing` to any nest,
  scores the rest with `ColonyState.TerrainSiteScore` (the terrain and food terms of `SiteScore`, food from `ChunkFoodIndex.NestScarcity`) and takes the best;
  terrain that is not loaded counts as unknown and is used only without a scored candidate.
  Each has its own fresh brain and genome and never takes the shared `fallback` model; a saved world restores its colonies unchanged.
- Reseed (`ColonyPopulation.CheckExtinctions`, `ReseedColony`): rule `reseedMode` (`ReseedMode`): `CloneBest` (default;
  copy of the best same-arm donor's brain and genome, founders from its mutated genes),
  `MutatedClone` (the same plus Gaussian weight noise of `reseedWeightSigma` times each layer's weight RMS, `SharedHierarchicalBrain.AddWeightNoise`;
  weights only, not taus, biases or the reservoir), `Fresh` (new brain of the same architecture, `ColonyState.ResetGenome`, default founders).
  `ColonyState.BrainDonorId` is the donor colony of the last replacement (0 for fresh); `BrainOrigin` is `reseeded` or `fission`, followed by `_mutated` or `_fresh` for those modes.
  The old brain is disposed after every agent that still uses it (a guest of another colony) is re-pointed to the new one.
- Fission (`fissionEnabled`, `fissionMembers`, `fissionShare`): when an extinct slot waits for reseed,
  `PickFissionParent` takes the same-arm colony with the most adults above `fissionMembers` (`ColonyState.AdultCount`) instead of the best colony;
  the slot is reseeded from it in the selected mode and `fissionShare` of its members are moved (`ColonyPopulation.MoveMember`, rebound to the new brain, placed near the new nest).
  Without a parent the ordinary reseed runs.
  No branch on caste or player.
- `ColonyPopulation.MoveMember(model, to, bindToTargetBrain)`: the one place that changes an agent's colony:
  leader cleared, caste bucket and member slot moved, `EntityModel.Rebind` (interrupts plan and command, sets `Colony` and `Nest`), `SpeechBus.SetColony`,
  skill block reset against the new colony graph, grid marked stale, position near the new nest.
  The brain binding is separate: `BrainComponent` holds its own brain reference, so an agent can live in colony B and decide with the brain of colony A.
- Migration votes require dry ground within the configured distance band. The final site must be dry and within one nest radius of the agent committing the move.
- Colony genome (`ColonyGenome` in `Assets/Scripts/EntitySystem/AI/Colony/ColonyGenome.cs`):
  `BaseLearningRate`, `MaxGradientNorm`, `EntropyRegularization`, the genes that only the shared training reads (`DelayedPerceptron.EnqueueTraining`).
  `ColonyState` starts it at the middle of the `GeneticParameters.ParameterRanges` of each gene, moved towards a random point by `colonyGenomeInitialSpread` (0 by default,
  so colonies do not differ before selection;
  1 is the full range), and pushes it to the brain (`SharedHierarchicalBrain.SetLearningGenes`) for a new brain and in `ReplaceBrain`;
  when an extinct colony is re-founded from the best donor colony of the same arm (`ColonyPopulation.PickDonor`, `ReseedColony`;
  without such a donor the colony keeps its brain and gets default founders) `ColonyState.InheritGenome` copies the donor genome with mutation (`colonyGenomeMutationChance`,
  step from `ParameterRanges`).
  The per-agent copies of these three genes stay in `GeneticParameters` but the training no longer reads them while the brain has a colony genome.
  Behavioural genes (including `SoftmaxTemperature` and `EntropyAlpha`) stay per agent.
- `ColonyState.Pressure`: 0..1, raised by damage from a foreign attacker and by `Killed` deaths, decays with `pressureTau`.
  It raises the soldier stimulus in `EntityManager.ChooseCaste`, lowers the vote weight the quorum needs (`pressureQuorumRelief`),
  adds to the nest danger term of migration and to the `Migrate` plan bias.
- Wild colony: `ColonyState.PackCentre` and `PackCount` are refreshed once per window by `EntityManager.UpdatePack`;
  the instinct `InstinctBits.Pack` pulls a creature farther than `packRadius` back toward the pack (`DirectionKind.Kin`);
  predators spawn within `predatorPackSpread` of a pack member;
  boid weights come from `packBoid*`.
  `CasteModifiers.Fear` (`predatorFearMul`) scales outnumbered, startle and foreign-harm stress; `CasteModifiers.HuntBias` (`predatorHuntBias`) biases the `Hunt` plan.
- `NestState` (`Assets/Scripts/EntitySystem/AI/Nest/NestState.cs`): channel fields in `NativeArray`, read through `ColonyFieldView`.
- Channels (`ColonyChannel`): `Food`, `Danger`, `Trail`, `KinDeath`, `Explored`, `Sickness`, `Territory`.
- `ColonyPoiBoard`: shared points of interest; `Offer` merges, `ReportEmpty` lowers `EnergyEma` of a point within the merge radius and never creates one.
  `ColonyStats`: counters per colony.
  `EnergyLedger`: energy flows of the world.
- `SeasonClock`: season value and winter test from `SimulationRules.Frame`.
- `CasteThresholdSystem` (`Assets/Scripts/EntitySystem/AI/Caste/CasteThresholdSystem.cs`): per-agent caste thresholds, SoA.
- `ColonyFood`: food regrowth and corpses. `EvolutionService`: fitness statistics, partner selection, crossover.
- `InfectionCoordinator`: wrapper over `InfectionService`; see `docs/map/virology.md`.
