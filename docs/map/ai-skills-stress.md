# AI: skills and stress

Root: `Assets/Scripts/EntitySystem/AI/`. Assembly: `EntitySystem`. Agent map: `docs/map/ai.md`.
Tests: `Assets/Tests/EditMode/` — `SkillGraphTests`, `SkillGraphAgentTests`, `StressTests`, `SocialReliefTests`, `HelpLogTests`.


## Skills (`Assets/Scripts/EntitySystem/AI/Skills/`)

Design: `docs/design/skill_graph.md`. Rules: `SimulationRules.Skills.cs`; `RulesFrame` mirrors the fields the math reads.

- `SkillAtlas`: ScriptableObject, one per world; points, `PotentialRow` adjacency masks, per-skill curve and XP cost, XP table by `EntityAction`.
  `SkillAtlas.Active` is bound by `SkillAtlas.Bind`; unbound it is `SkillAtlasBuilder.BuildDefault()` with a fixed seed, so every machine builds the same atlas.
- `SkillAtlasBuilder`: Poisson points, Bowyer–Watson Delaunay, second diagonals by cocircularity, K5 check (`HasK5`).
  The editor menu `Tools/Ravine/Skills/Build Atlas` is `Assets/Scripts/Editor/SkillAtlasMenu.cs`.
- `SkillGraphTopology` (+ `.Tarjan`): `Active` rows, `IslandMask`, `PrunedMask`, budget;
  `Create`, `TryBuild`, `TryPrune`, `TryRestore`;
  one iterative Tarjan pass rebuilds bridges, `CliqueSize`, `CycleFactor`, `ComponentOf`.
- `SkillGraphMath`: static rules 1–12 of the design, Burst-compatible, rules by `in RulesFrame`.
- `SkillGraphStore`: per-agent SoA (`Levels`, `Peaks`, `LastUsed`, `Talents`, `Roots`, `Dirty`); `Reset`, `Retarget`, `AddXp`, `Materialize`, `RemoveSwapBack`.
- `SkillGraphQuery.Fill` writes a `SkillGraphSnapshot` for a view through `ISkillGraphSource`; `ISkillGraphCommands` is the command side (`PlayerSkillGraph` implements both).
  View: `docs/map/inventory-ui.md`.
- `SkillModifiers`: one multiplier per `SkillChannel`; `From(store, index, topology, atlas)`.
- `SkillGraphXp.Award` adds the XP of a completed `EntityAction`; `EntityCommandRunner.Finish` calls it through `EntityModel.AwardSkillXp` for every creature.
- `EntityModel.RefreshSkillTuning` re-expresses tuning at the start of a cycle when the store `Dirty` flag is set; `EntityTuning.Express` with `SkillModifiers` and `CasteModifiers.Combine`.
- `SkillDecayJob`: Burst sweep over a sliding window of agents from `EntityManager.SweepSkills`; reads per-colony cycle factors published by `SkillGraphStore.PublishColony`.
- `SkillGraphSolver.Step`: per-colony plasticity (build, prune) once per `SkillSolverInterval` from `EntityManager.SolveSkills`;
  `ColonyState.SkillTopology` is the colony graph, the wild colony owns one too.
- Skill genes (`SkillOriginX/Y`, `Plast*`) are phenotypic: `CopyLearningGenes` never overwrites them. Saved brains do not hold genes.
- `SkillAtlas.Skill` is a graph vertex; the ability skills of `Assets/Scripts/Model/EntitySkills/` (`SkillComponent`) are unrelated.

## Stress

- `EntityModel.Stress` (0..1, raw) is raised by `AddStress(amount, source)` and decays in `RunCycleHead` (`stressTau`, faster at the nest).
  `ColonyStats.RecordStress` adds the amount actually added (after saturation) to the source `Threat` (startle, fire, outnumbered, foreign harm, kin death, leader succession), `Hunger`,
  `Cold`, `Lonely` or `Task`.
- With `stressSourcesEnabled` (`EntityModel.Stress.cs`, `UpdateStress`, called in `RunCycleTail` after the environment drain multiplier):
  hunger `stressHungerRate × (1 − energy / stressHungerThreshold)` below the threshold;
  cold `stressColdRate × max(0, EnvironmentDrainMul − 1)` outside the nest at night or in winter;
  loneliness `stressLonelyRate` once the agent has had no kin within `lonelinessRadius` for `lonelinessWindow` seconds (`SurveyOutput.KinNear`,
  counted by the survey job within `SurveyInput.KinRadius`, set only with the flag;
  timer `EntityModel.LonelySeconds`);
  task failure `stressTaskFailure` when a plan ends `Failed` (`PlanRunner.End` → `NoteTaskFailure`).
  All rates are per second of cycle `dt`.
- `StressSmooth` is an EMA of the raw value (`stressSmoothTau`).
  `StressLevel` (0 calm, 1 stressed, 2 high) uses two pairs of thresholds with hysteresis on the smoothed value: `stressStressedEnter`/`Exit`, `stressHighEnter`/`Exit`.
  `StressEffect` is the smoothed value while the level is above calm and 0 otherwise or with the flag off; all consequences read it.
- Consequences (formulas in `CreatureCore`, all neutral at effect 0): basal drain `× (1 + stressDrainMul × s)`;
  detection radius `× (1 − stressRadiusShrink × s)`;
  discount `gamma ^ (1 + stressDiscountMul × s)` per agent through `EntityBrainContext.Gamma` (coordinator and executor TD, goal return, terminal flush, plan return);
  learning rate of each training ticket `× max(stressLrFloor, 1 − stressLrMul × s)` through `PerceptronContext.LearningScale` (the colony genome is not touched);
  plan abandon chance `stressAbandonChance × s` at each plan step (the plan ends `Interrupted`).
  `EntityBrainContext.SetStressScales` is written every cycle.
  The input size is unchanged.
- Social relief (`socialReliefEnabled`;
  the pair rules need `helpLogEnabled`): `EntityModel.RelieveStress(amount, kind)` lowers the raw stress and books the amount in `ColonyStats.ReliefSum` (`Comfort`, `Feed`, `Rest`, `Nest`).
  `Nest` is the extra decay at the nest over the ordinary `stressTau`, booked every tick whatever the flags.
- `GiveCommand`: with the flag on the branch order is feed (a kin in range needs food and the giver has some), groom (a sick kin), comfort (`EntityModel.FindMostStressedKin`:
  stress above `comfortMinStress`, pair cooldown, ties within `comfortTieEpsilon` broken by the help-log balance).
  Comfort costs `comfortEnergyCost` at the start (booked as `EnergyFlows.Social`) and `comfortDuration`;
  at the end, if the partner is still alive and in range, `SocialRelief.Comfort` gives the partner `comfortRelief × stress × (1 − accumulator / comfortPairCap)`
  (never above the remaining room under the cap), the giver `comfortSelfShare` of that, and updates both help logs and `AnomalyDetector.NoteRelief`.
  The accumulator is the pair's decayed relief over `comfortPairWindow`, kept in both logs, so a loop between two agents is bounded by `comfortPairCap`.
  With the flag off the old two branches run unchanged.
- Feeding: `SocialRelief.Feed` (called for every transfer, the player path would call the same method with the player's `CodexAuthor` id) writes the receiver's and giver's logs,
  counts the transfer (`GiveTransfers`, `GiveReciprocal`), calls `AnomalyDetector.NoteTransfer` and, with the flag on,
  relieves the receiver by `feedRelief × closed share of need × stress` (the giver gets `giveSelfShare` of it);
  a transfer is reciprocal when the same pair transferred the other way within `helpReciprocalWindow`, and then gives no relief.
  `giveTransferLoss` of every portion is lost and booked in `EnergyFlows.Social`.
  Without a help log the relief is not given.
- Joint rest: while the `Rest` plan runs, each `RestCommand` step relieves `min(restReliefCap, restReliefRate × resting kin)` per second;
  resting kin are those counted by `RestingKin` (neighbour query, kin running `Rest`), not the survey kin count.
- `HelpLog` (`Assets/Scripts/EntitySystem/AI/Social/HelpLog.cs`, only with `helpLogEnabled`, registered in the `ServiceLocator`):
  SoA `NativeArray<HelpEntry>` of `helpLogSize` entries per `ManagerIndex`;
  an entry holds the partner id (`EntityId`, `CodexAuthor.ForPlayer` for players), balance (received minus given, decayed lazily by `helpBalanceTau`),
  comfort accumulator and the times of the last comfort, gift given, gift received and meeting.
  A new partner replaces the free slot or the entry with the oldest meeting.
  `Reset` at spawn, `RemoveSwapBack` in `ColonyPopulation.RemoveEntitySwapBack`, saved in the agent record.
- `PlanHint.KinStressed` (bit 34) is set while the nearest creature is a stressed kin; it only opens `Give` in the `Court` plan.
- The player has no stress state; `PlayerEntity` still calls `CreatureCore.EnvironmentDrainMul` without it.
