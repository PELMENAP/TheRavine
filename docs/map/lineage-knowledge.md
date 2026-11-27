# Lineage and knowledge
Roots: `Assets/Scripts/EntitySystem/AI/Lineage/`, `Assets/Scripts/EntitySystem/AI/Knowledge/`. Assembly: `EntitySystem`.
Rules: `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.Lineage.cs`, `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.Knowledge.cs`.
Tests: `Assets/Tests/EditMode/LineageTests.cs`, `Assets/Tests/EditMode/KnowledgeTests.cs`, `Assets/Tests/EditMode/SoaSwapBackTests.cs`.

## Entry points
| Type | File | Role |
|---|---|---|
| `LineageStore` | `Assets/Scripts/EntitySystem/AI/Lineage/LineageStore.cs` | Two-parent records by entity id; `Relatedness`; held by `ColonyPopulation.Lineage` |
| `MateLoci` | `Assets/Scripts/EntitySystem/AI/Lineage/MateLoci.cs` | 32 inherited compatibility loci: `Cross`, `Mutate`, `Pair`, `Conflicts`, `Viability`, `Divergence` |
| `GeneticDistance` | `Assets/Scripts/EntitySystem/AI/Lineage/GeneticDistance.cs` | Normalised distance of two `GeneticParameters` over fixed gene ranges |
| `KnowledgeSystem` | `Assets/Scripts/EntitySystem/AI/Knowledge/KnowledgeSystem.cs` | Per-agent SoA: observation ring, best and taught expression, `Observe`, `Search`, `Teach`, `Predict` |
| `KnowledgeMath` | `Assets/Scripts/EntitySystem/AI/Knowledge/KnowledgeMath.cs` | Postfix expressions of at most 16 nodes: `IsValid`, `Evaluate`, `Mutate`, `ModelCost`, `DataCost` |

## Lineage data flow
1. `ColonyPopulation.RegisterEntity` writes `LineageStore.Birth` for every spawn and restore; `ProcessPendingDeaths` writes `Death`.
2. `EntityModel.MateId` and `EntityModel.MateLoci` are set once in `RegisterEntity` (`SetLineage`); founders have loci 0.
3. With `mateLociEnabled`, `SpawnChild` crosses the parent and mate loci, mutates them and rolls `Viability` of the `Conflicts` count.
4. An inviable embryo costs the parent the reproduction energy (`EnergyFlows.Reproduce`) and counts as `BirthFailure.Inviable`.
5. Conflicts are Dobzhansky–Muller pairs: a pair hurts only when both derived alleles meet, so diverged lines clash only in hybrids.
6. `EvolutionService.LociDivergence` compares sampled allele frequencies of two colonies (diagnostic only).
7. Population checkpoint version 4 stores `Mate` and `MateLoci` per agent.

## Knowledge data flow
1. `EntityModel.StepKnowledge` runs in `RunCycleTail` after `UpdateStress`, only with `knowledgeEnabled`.
2. Every `knowledgeObserveInterval` the agent records its own features (day phase, season, at nest, food proximity, energy, stress).
3. The target of a sample is the energy rate measured until the next sample, so a prediction never sees its own outcome.
4. `Search` mutates the best expression (or tries a taught one), fits on the oldest 75 % and confirms on the newest 25 %.
5. Score is `ModelCost + DataCost` (MDL proxy); acceptance needs a `knowledgeMinGain` gain on fit data and no loss on held-out data.
6. Reward is the held-out improvement over the incumbent, bounded by `knowledgeRewardCap`, and only when it beats the mean baseline.
7. `Teach` copies the best expression to the nearest kin within `knowledgeTeachRadius` with copy errors; the receiver revalidates it.

## Invariants
- No sensor reads lineage records, relatedness or loci; they are diagnostics and inheritance only.
- The knowledge reward is extrinsic reward; input and output sizes of the brain are unchanged.
- `KnowledgeSystem.Search`, `Observe` and `Teach` allocate nothing after construction.
- `KnowledgeSystem` exists only when `knowledgeEnabled` was set at `EntityManager.Awake`.

## Hot paths
`EntityModel.StepKnowledge` is part of the agent tick: `docs/map/HOT_PATHS.md`, section Agent tick.
