# Skill graph — stage prompts

Four prompts in the format of `docs/PROMPT_TEMPLATE.md`. Send them one at a time, in order; each stage compiles and
is testable on its own. The shared specification is `docs/design/skill_graph.md` (called "the design" below).

---

# Stage 1. Skill atlas, topology and math core (no integration)

## RULES

Follow `CLAUDE.md` of the repository: language, philosophy, project constraints, task procedure, verification, response format.
Read first: `docs/design/skill_graph.md`, `docs/map/ai.md`, `docs/map/README.md` (structural rules for new code).
Read `docs/OPTIMIZATION.md` sections 2, 13, 14, 15: the code of this stage is called from the agent tick in stage 2.

## CONTEXT

Task: implement the data and pure rules of the skill graph from the design, sections 3 and 4, with EditMode tests.
Nothing in the game calls this code yet.

Known facts (line numbers are approximate, verify against the file):
1. Rules are partial files `SimulationRules.<Area>.cs` in `Assets/Scripts/EntitySystem/AI/Rules/`; tick-read values
   are mirrored in `RulesFrame` (`SimulationRules.cs:10`) and captured by `CaptureFrame` (`:1135`).
2. `Assets/Scripts/Model/EntitySkills/` and the assembly `SkillSystem` already exist for ability skills; do not touch
   them and do not name a new type `SkillSystem`.
3. The reference shape of a small native container is `Assets/Scripts/EntitySystem/AI/Caste/CasteThresholdSystem.cs`.
4. AI types next to the new folder are in the global namespace.

## TASK

One stage. Perform the items strictly in order.

### Item 1. Rules
Files: new `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.Skills.cs`; `SimulationRules.cs` (`RulesFrame`).
- Add one field per constant of the design section 4 with the stated default: `SkillTalentMin`, `SkillTalentDecay`,
  `SkillTalentEuclidShare`, `SkillPhiRefLevel`, `SkillEdgeCostScale`, `SkillEdgeCostExp`, `SkillClique3Boost`,
  `SkillClique4Boost`, `SkillCycleMinFactor`, `SkillCycleScale`, `SkillLogicScale`, `SkillLogicOffset`,
  `SkillPruneFocusScale`, `SkillDecayRate`, `SkillSavingsFloor`, `SkillXpLevelOffset`, `SkillSolverNormLevel`,
  `SkillSolverIslandBonus`, `SkillPruneUnusedLevel`, `SkillPruneMinSize`, `SkillSolverInterval`, `SkillSweepInterval`,
  `SkillIslandDropMax`, `SkillForestGeneRange`, `SkillK4Quantile`.
- Mirror in `RulesFrame` only the fields read by the XP event and the decay sweep.
- Done when: `RulesDefaultsTests` style check for the new defaults exists and no constant of the design is a literal elsewhere.

### Item 2. Atlas
Files: new folder `Assets/Scripts/EntitySystem/AI/Skills/` — `SkillAtlas.cs`, `SkillAtlasBuilder.cs`, `SkillChannel.cs`,
`SkillNeed.cs`, `SkillCurve.cs`; new `Assets/Scripts/Editor/SkillAtlasMenu.cs`.
- `SkillAtlas` is a ScriptableObject with the content of the design section 3.1 and a static `Active` bound like
  `SimulationRules.Bind`; `SkillAtlas.BuildDefault()` returns an in-memory atlas built from the table of the design
  section 5 when no asset is bound.
- `SkillAtlasBuilder` implements the four build steps of section 3.1. It is editor / load-time code: clarity over speed.
- `SkillAtlasMenu` adds Tools/Ravine/Skills/Build Atlas, which writes the asset through `AssetDatabase`.
  Do not hand-write the `.asset` file.
- Done when: `BuildDefault()` yields N = 18 points inside the unit square, a symmetric `Potential` matrix without
  self-loops, and the K5 check of section 3.1 passes.

### Item 3. Topology
Files: `Assets/Scripts/EntitySystem/AI/Skills/SkillGraphTopology.cs`, `SkillGraphTopology.Tarjan.cs`.
- Fields of the design section 3.2; `IDisposable` with `IsCreated` checks.
- `Create(atlas, forestGenes)`: Kruskal forest and islands as specified. `TryBuild(u, v)`, `TryPrune(u, v, rootSide)`,
  `TryRestore(u, v)` return `false` without changing state when the rule of section 4 items 5 and 14 refuses.
- After every accepted change: one iterative Tarjan pass (no recursion, no managed allocation, scratch buffers owned
  by the topology), then `CliqueSize`, `CycleFactor`, `ComponentOf`, `Revision++`.
- Done when: tests show a tree has no cycle factor below 1; one added edge forming a triangle gives clique 3 for
  exactly three vertices; three added edges forming K4 give clique 4; a bridge is reported only for edges outside cycles.

### Item 4. Math and store
Files: `Assets/Scripts/EntitySystem/AI/Skills/SkillGraphMath.cs`, `SkillGraphStore.cs`, `SkillModifiers.cs`.
- `SkillGraphMath`: static, Burst-compatible functions for section 4 items 1–12; inputs by `in`, rules by
  `in SimulationRules.RulesFrame`; `Unity.Mathematics` only.
- `SkillGraphStore(capacity, skillCount)`: buffers of section 3.3; `Reset(index, in genes, topology, atlas)` computes
  root and talent; `AddXp(index, skill, xp, now, topology, atlas, in rules)` implements item 11 and returns whether
  `⌊L⌋` changed; `RemoveSwapBack(int index, int last)` copies stride blocks with `NativeArray<T>.Copy`.
- `SkillModifiers`: one float per `SkillChannel`, `Neutral`, and `From(store, index, topology, atlas)` (item 12).
- Do not add jobs in this stage.
- Done when: tests cover — talent is 1 at the origin vertex and never below `SkillTalentMin`; 64 skills at peak 1
  earn 0 edge points; decay never crosses `SkillSavingsFloor · peak`; a skill fallen from peak 100 to 25 with no
  neighbours gets a pull above 1.6; `AddXp` on an unavailable skill changes nothing; swap-back moves every buffer
  (add the case to `SoaSwapBackTests`); `AllocationAssert.Zero` passes for `AddXp`.

### Item 5. Maps
Files: `docs/map/ai.md` (section "Skills" with the new entry points), generated maps.
- Run `python3 tools/docmap.py gen` and `check`.

## BRANCH

Push to the session branch.

## DEFERRED
- Final skill list, coordinates and curve profiles of the atlas are the author's content decision.
- Relation of graph skills `Flight` / `Swimming` to `FlyingSkill` / `SwimmingSkill` (energy cost, cooldown scaling).

---

# Stage 2. Agents and colonies

## RULES

Follow `CLAUDE.md`. Read first: `docs/design/skill_graph.md`, `docs/map/ai.md`, `docs/map/HOT_PATHS.md`,
`Assets/Scripts/EntitySystem/AI/CLAUDE.md`. This stage is on a hot path: read `docs/OPTIMIZATION.md` and follow its procedure.

## CONTEXT

Task: give every agent a skill graph: genes, XP from completed actions, effects through tuning, periodic decay,
colony topology and the plasticity solver. The brain's inputs and outputs do not change.

Known facts (verify line numbers):
1. `GeneticParameters` (`Assets/Scripts/EntitySystem/AI/GeneticParameters.cs`): flat floats, `Idx*` constants,
   `ParameterRanges` (`:91`), a `GeneCount` consistency check (`:155`), `Crossover` (`:198`).
2. Children get genes at `ColonyPopulation.cs:158`; `EvolutionService.cs:181` overwrites the genes of a living agent.
3. Per-agent systems are created in `EntityManager.Awake` (`:301`–`:308`) with `_capacity`.
4. `ColonyPopulation.RemoveEntitySwapBack` (`:264`) is the only removal path.
5. `EntityCommandRunner.cs:84` is where a completed action is recorded.
6. `EntityModel.cs:287` and `:563` call `EntityTuning.Express(in base, in genes, in casteMods)`.
7. `EntityManager.CloseTickWindow` (`:524`) calls `TickNests()` and `TickColonies()`.
8. `ColonyState` (`Assets/Scripts/EntitySystem/AI/Colony/ColonyState.cs`) is `IDisposable`, exposes `Members` and `Stats`.

## TASK

### Item 1. Genes
Files: `GeneticParameters.cs`.
- Append the six fields of the design section 3.4 with `Idx*` constants, ranges and mutation scales; defaults
  `(0.5, 0.5, 1, 1, 1, 1)`.
- Find every place that serialises or sizes `GeneticParameters`. If a saved format depends on the gene count, bump
  its version and state the incompatibility in the report. `SharedBrainSnapshot.Version` changes only in that case.
- Done when: the `GeneCount` check passes and existing genome tests are unchanged.

### Item 2. Store lifecycle
Files: `EntityManager.cs`, `ColonyPopulation.cs`, `ColonyState.cs`, `EvolutionService.cs`, `docs/OPTIMIZATION.md` section 14.
- `EntityManager` owns one `SkillGraphStore(_capacity, SkillAtlas.Active.Count)`; dispose it where the other systems are disposed.
- `ColonyState` owns a `SkillGraphTopology` created with forest genes drawn by `RavineRandom` inside
  `SkillForestGeneRange`; disposed in `ColonyState.Dispose`. The wild colony gets one the same way.
- On spawn call `Reset`; when `EvolutionService` overwrites genes call a re-rooting that recomputes root and talent
  and keeps levels and peaks.
- Add `_skills.RemoveSwapBack(idx, last)` to `RemoveEntitySwapBack` and the system to the registry of section 14.
- Done when: `SoaSwapBackTests` covers the store through the population path.

### Item 3. XP from actions
Files: `Assets/Scripts/EntitySystem/AI/Entity/Commands/EntityCommandRunner.cs`.
- Next to `RecordCompleted`, look up the atlas pairs of the action and call `AddXp` with `SimulationClock.Time`.
- No branch by caste, colony kind or creature type.
- Done when: an allocation test shows zero allocation for a completed command with XP.

### Item 4. Effects
Files: `Assets/Scripts/EntitySystem/AI/Entity/EntityTuning.cs`, `EntityModel.cs`.
- Add an `Express` overload that also takes `in SkillModifiers` and multiplies the same fields the caste modifiers do.
- `EntityModel` re-expresses its tuning when its `Dirty` flag is set, at the start of its next cycle, and clears the flag.
- Done when: a test raises a `Strike` level and observes a larger `Tuning.AttackDamage`, with other fields equal.

### Item 5. Sweep and solver
Files: new `Assets/Scripts/EntitySystem/AI/Skills/SkillDecayJob.cs`, `SkillGraphSolver.cs`; `EntityManager.cs`.
- `SkillDecayJob` (Burst, `IJobParallelFor` over agents): rule 8 for every skill, sets `Dirty` on an integer change.
  Scheduled from `CloseTickWindow` over a sliding window so every agent is visited once per `SkillSweepInterval`;
  one `Complete`, no resize inside the job.
- `SkillGraphSolver.Step(colony, store, atlas, rules)`: rule 15, once per `SkillSolverInterval` per colony; mean
  levels and mean `Plast*` genes are accumulated over `colony.Members` into scratch buffers owned by the solver.
  `need(v)` reads existing `ColonyStats` fields only; list in the report which field feeds which `SkillNeed`.
- Topology changes follow rule 13.
- Add `ProfilerMarker`s for the sweep and the solver and the methods to `docs/map/HOT_PATHS.md`.
- Done when: a test colony with free points builds an edge and its `Revision` grows; marker times are in the commit message
  if the editor was available, otherwise the report says they were not measured.

### Item 6. Maps
Run `python3 tools/docmap.py gen` and `check`; update `docs/map/ai.md`.

## BRANCH

Push to the session branch.

## DEFERRED
- Colony topology and agent levels are not saved: only the brain is persisted today.
- A per-individual topology for wild creatures instead of the shared wild colony topology.
- Columns for skills in `SimulationLog` and a page in `SimulationDebugDashboard`.

---

# Stage 3. Player and network

## RULES

Follow `CLAUDE.md`. Read first: `docs/design/skill_graph.md`, `docs/map/player-net.md`, `docs/map/core.md` (Saves).
This stage adds RPCs: read `docs/SECURITY.md` and follow its procedure; add rows to its section 14.

## CONTEXT

Task: the player uses the same skill graph rules as an agent: its own store of capacity 1 and its own topology,
XP from the same action table, effects on its stats, and server-validated topology commands.

Known facts (verify line numbers):
1. `PlayerEntity.AddCreatureComponents` (`Assets/Scripts/EntitySystem/Entityes/Player/PlayerEntity.cs:47`) uses
   `GeneticParameters.Default` and the shared `CreatureCore`.
2. The player ticks in `EntitySystem.FixedUpdate`; metabolism runs only where `WorldAuthority.IsAuthority`.
3. Server handlers of player actions are in `PlayerController.cs` (over 500 lines: use `docs/map/LARGE_FILES.md`);
   budgets use `RateBudget`.
4. `WorldState` (`Assets/Scripts/Core/Settings/WorldData/WorldState.cs`) is saved with MemoryPack.

## TASK

### Item 1. Player graph
Files: `PlayerEntity.cs`, new `Assets/Scripts/EntitySystem/Entityes/Player/Components/PlayerSkillGraph.cs`.
- `PlayerSkillGraph` is an `IComponent` owning `SkillGraphStore(1, N)` and a `SkillGraphTopology`; disposed with the entity.
- Skill genes of the player: drawn once inside `ParameterRanges` when the world state has none, then saved.
- Decay sweep and optional solver step run on the authority from the player's tick with `SimulationClock.Time`,
  at the same intervals as for agents, calling the same `SkillGraphMath` and `SkillGraphSolver` code.
- Done when: no rule or constant differs from the agent path; name every `[SerializeField]` added in the report.

### Item 2. XP and effects
Files: `PlayerController.cs`, `PlayerEntity.cs`.
- On the authority, after a validated action succeeds, award XP through the atlas row of the matching `EntityAction`
  (movement distance → `GoToPoint`, jump or sprint → `Flee`, pick-up → `PickUp`, placement → `StoreFood`,
  speech → `Speech`). If an action of the player has no matching `EntityAction`, list it in DEFERRED and skip it.
- Apply `SkillModifiers` to the stats the player already has (movement speed, maximum energy, maximum health)
  where they are computed today; do not add new stats.
- Done when: the success path of every touched RPC handler stays free of allocation and formatting.

### Item 3. Commands and replication
Files: `PlayerController.cs`, `docs/SECURITY.md` section 14.
- `SkillGraphCommandServerRpc(byte op, byte u, byte v)`: op is build, prune, restore or auto-mode toggle. Server
  checks: indices below N, edge in `Potential`, the rule of design section 4 items 5 and 14, active and alive;
  budget through `RateBudget`. Identity from `OwnerClientId`.
- Owner-only replication, skipped when unchanged: topology (`Active` rows, masks, budget) on a `Revision` change;
  levels and peaks as 16-bit fixed point at a rules-defined interval.
- The host applies commands directly through the same validation function.
- Done when: both RPCs have rows in section 14 and a test feeds out-of-range and rule-breaking arguments to the
  validation function and gets a refusal without state change.

### Item 4. Save
Files: `WorldState.cs` and the code that fills it before a save.
- Append the player's skill genes, levels, peaks, built edges, pruned bridges, `PhiPeak` and `SpentEdgePoints`.
  On load, validate sizes and finiteness; on a mismatch with the atlas start a fresh graph and log once.
- Done when: an old save without the new members still loads.

### Item 5. Maps
Run `python3 tools/docmap.py gen` and `check`; update `docs/map/player-net.md`.

## BRANCH

Push to the session branch.

## DEFERRED
- Remote players' graphs are session-only until a stable player identity exists (open issue in `docs/map/player-net.md`).
- A player who joins a colony: whose topology applies.

---

# Stage 4. In-game graph view (UI Toolkit)

## RULES

Follow `CLAUDE.md`. Read first: `docs/design/skill_graph.md` section 6, `docs/map/inventory-ui.md`, `docs/map/terminal.md`.
UI is not a hot path (`docs/OPTIMIZATION.md` section 17): write for clarity; the cost must be zero while the panel is closed.

## CONTEXT

Task: an in-game view of the local player's skill graph on runtime UI Toolkit: pan and zoom, fog of war, node and
edge states, build / prune / restore commands, auto-mode toggle.

Known facts:
1. The project has no UI Toolkit code; existing UI is uGUI in the assembly `UIInventory` (`Assets/Scripts/UI/`),
   which already references `EntitySystem`.
2. `UnityEditor.Experimental.GraphView` is editor-only: do not use it. Build the view from `VisualElement`,
   `generateVisualContent` and `Painter2D`.
3. Scene and asset YAML may not be hand-edited, and `GameInput.cs` is generated: the panel is created at runtime
   and opened by a terminal command registered in `Terminal.Setup` (`Assets/Scripts/Terminal/Terminal.cs:71`).
4. `ActionMapController` (`Assets/Scripts/Core/NewInputSystem/ActionMapController.cs`) switches input maps
   (`SwitchToInventory`, `SwitchToGameplay`, `EnableUI`, `DisableUI`).
5. `Assets/Resources/` exists and is used for runtime-loaded assets.

## TASK

### Item 1. Snapshot and commands
Files: new `Assets/Scripts/EntitySystem/AI/Skills/SkillGraphSnapshot.cs`, `SkillGraphQuery.cs`, `ISkillGraphCommands.cs`;
`PlayerSkillGraph.cs`.
- Implement the snapshot of design section 6. `SkillGraphQuery.Fill` applies the fog rule (design section 4 items 3
  and 4): hidden vertices and edges are not written. On a remote client it reads the replicated state of stage 3.
- `PlayerSkillGraph` implements `ISkillGraphCommands` (`CanBuild`, `Build`, `CanPrune`, `Prune`, `CanRestore`,
  `Restore`, `AutoMode`), routing to the direct call on the host and to the RPC on a client.
- Done when: a test fills a snapshot for a fresh graph and finds the root open, its active neighbours available,
  its potential neighbours as silhouettes, and nothing else.

### Item 2. Canvas and nodes
Files: new `Assets/Scripts/UI/SkillGraph/SkillGraphCanvas.cs`, `SkillNodeElement.cs`, `SkillGraphPalette.cs`;
new `Assets/Resources/UI/SkillGraph.uss`.
- `SkillGraphCanvas : VisualElement`: maps unit-square positions to the content rect; pan by pointer drag with
  capture, zoom by `WheelEvent` around the pointer, clamped; edges in `generateVisualContent`: active solid, bridge
  thicker, potential dashed from short segments; `MarkDirtyRepaint` only on a snapshot or transform change.
- `SkillNodeElement`: pooled, absolutely positioned; progress ring by `Painter2D.Arc`; classes for `Open`,
  `Available`, `Silhouette`, `Island`, `Pruned`; clique size and decay factor as small badges; hover card with
  name, level, peak, floor, talent and the three XP multipliers.
- Edge picking: nearest segment within a pixel tolerance in the canvas' pointer handler.
- Colours and sizes live in the USS file and `SkillGraphPalette`; no simulation constant is duplicated in UI.
- Done when: the element renders from a hand-built snapshot in an EditMode test that only checks child counts and classes.

### Item 3. Panel and opening
Files: new `Assets/Scripts/UI/SkillGraph/SkillGraphPanel.cs`, `Assets/Scripts/Terminal/SkillGraphCommand.cs`; `Terminal.cs`.
- `SkillGraphPanel`: `MonoBehaviour` with `[SerializeField] PanelSettings` and a runtime fallback
  (`ScriptableObject.CreateInstance<PanelSettings>()`, explicit font through `style.unityFontDefinition`); builds the
  tree in C#: header with `available / earned` edge points and `PhiPeak`, the canvas, a side card, the auto-mode toggle,
  a confirmation overlay for prune.
- Open and close switch the input map through `ActionMapController` and start or stop a `schedule.Execute` refresh
  that compares `Revision` and a level checksum before rebuilding.
- `SkillGraphCommand` (`skills`): finds or creates the panel object and toggles it. It is a normal command, not a debug one.
- Done when: with the panel closed no code of this stage runs per frame; state in the report that the visual result
  was not checked in the editor.

### Item 4. Maps
Run `python3 tools/docmap.py gen` and `check`; add a "Skill graph view" section to `docs/map/inventory-ui.md` and the
command to `docs/map/terminal.md`.

## BRANCH

Push to the session branch.

## DEFERRED
- A `PanelSettings` asset with a runtime theme and a key binding in `InputAction.inputactions` must be created by
  the author in the editor.
- Viewing the graph of a selected colony or agent from `SimulationDebugDashboard`.
- Touch gestures (pinch zoom) for the mobile control type.
