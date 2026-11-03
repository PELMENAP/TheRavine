# Action census and consolidation proposal

Snapshot: commit `66ba120` (branch `claude/practical-mendel-c5lbxd`). Offline audit, no code change. Paths are relative to `Assets/Scripts/EntitySystem/AI/` unless they start with `Assets/`.

## 1. Census

### 1.1 Common machinery

- Enum: `EntityAction.cs:1-8`, 20 values, `Idle = 0` to `Groom = 19`.
- Table: `ActionCatalog.cs:44-68` (`BuildTable`). Flags `ActionCatalog.cs:4-12`. `NoSoldier` `ActionCatalog.cs:40`.
- RL subsets: `ActionCatalog.cs:94-116`. An action enters the subset of goal `g` if its goal mask has `g` and it is neither `InstinctOnly` nor `PlanOnly`. Sizes: Survive 8, Hunt 5, Forage 8, Social 7; 18 distinct actions (all except `Follow`, `MoveNest`).
- Command objects: one per action, created in `Entity/EntityModel.cs:527-546`.
- Generic start paths (any action, by index):
  - brain: `Entity/EntityModel.Cycle.cs:447` (`TryStartCommand(decision.Action, …, CommandSource.Brain)`), masked by `PlanCatalog.EntryMask & CasteMask` (`SharedAI/SharedHierarchicalBrain.cs:475`);
  - plan step: `Plans/PlanRunner.cs:148`;
  - instinct reflex: `Entity/EntityModel.Behaviour.cs:40`;
  - predator (wild) controller: `Entity/EntityModel.Wild.cs:91` (`StartWild`).
  - All go through `Entity/EntityModel.Behaviour.cs:60-74` (`CanExecute`, then `SetLastAction`, then `EntityCommandRunner.Start`).
- Start counting: `Entity/Commands/EntityCommandRunner.cs:37` calls `ColonyStats.RecordAction` (`Colony/ColonyStats.cs:58`) into `ColonyStats.ActionsStarted` (`Colony/ColonyStats.cs:18`).
- Per-action durations: hard-coded positional arrays `SharedAI/ActionDurationTable.cs:3-15` (20 entries each).
- Instinct priority per action: `Instinct/InstinctSystem.cs:247-258` (switch on action).
- Movement commands derive from `PlannedMoveCommand` (`Entity/Commands/PlannedMoveCommand.cs:3-48`). They spend `En` indirectly: motion cost goes through `Motor.DrainEnergy()` into `StatsComponent.Tick` (`Entity/EntityModel.Cycle.cs:225-227`).

### 1.2 Usage numbers

`ColonyStats.ActionsStarted` exists and counts every start, but it is kept only in memory. Neither the Stage 1 energy ledger nor anything else writes it to disk, and the repository holds no recorded run. **No usage numbers are available.**

### 1.3 Per-action table

"Call sites" lists every `EntityAction.<Name>` reference outside `ActionCatalog.cs`, from `grep -rn "EntityAction\.<Name>\b" Assets`. `S` marks a site that starts the action (or offers it in a mask); `C` marks a site that only compares or reads it. The generic paths of 1.1 are not repeated. "Reads / writes" lists properties of the Stage 1 matrix plus action-specific state, with lines in `Entity/Commands/<Name>Command.cs` unless stated.

| # | Action | Command class | Goal mask | Caste mask | Flags | Call sites (count) | Reads | Writes |
|---|---|---|---|---|---|---|---|---|
| 0 | Idle | `IdleCommand` | Survive, Hunt, Social | all | none | 5: S `Entity/EntityModel.Wild.cs:53,64`; C `Entity/EntityModel.Wild.cs:35`, `Entity/EntityModel.Cycle.cs:220`; registration `Entity/EntityModel.cs:527` | decision end time 12 | motor stop 7 |
| 1 | Wander | `WanderCommand` | Survive, Forage | all | IsMove | 18: S `Instinct/InstinctSystem.cs:177`, `Plans/PlanCatalog.cs:155,170,176,177,241,269,289`, `Entity/EntityModel.Behaviour.cs:54`, `Entity/EntityModel.Wild.cs:82,84`; C `Instinct/InstinctSystem.cs:252`, `Plans/PlanCatalog.cs:228,268,283`, `Plans/PlanRunner.cs:147`, `Entity/EntityModel.Wild.cs:35`; registration `Entity/EntityModel.cs:528` | nest migration 72-75; plan destination 77-86; direction head 87; colony field gradient 109-118; terrain (`LastTerrain`) 104; `Caste` 128; `Colony` 129 | movement (`En` via motion); colony quorum vote `VoteNestSite` 135 (Scout only) |
| 2 | RememberPoint | `RememberPointCommand` | Forage | all | none | 2: S `Plans/PlanCatalog.cs:235`; registration `Entity/EntityModel.cs:529` | position 9; `Caste` 11; food visibility 12 | own PoI memory `Points.TryRemember` 10; colony PoI board `Pois.Offer` 12 (Scout only) |
| 3 | GoToPoint | `GoToPointCommand` | Survive, Forage | all | IsMove, NeedsTarget | 8: S `Instinct/InstinctSystem.cs:190`, `Plans/PlanCatalog.cs:155,240`; C `Instinct/InstinctSystem.cs:255`, `Plans/PlanCatalog.cs:228,229,239`; registration `Entity/EntityModel.cs:530` | recruit target 16-19; direction head 21-24; own and colony PoI (`EnergyEma` via `TryPickBest`) 22, 24, 27-34; `Colony` 8, 23-28 | movement; PoI visit 45; recruit cleared 46; negative PoI mark 47 |
| 4 | Attack | `AttackCommand` | Hunt | all | IsMove, NeedsTarget, EnergyGated | 6: S `Plans/PlanCatalog.cs:159,248`, `Entity/EntityModel.Wild.cs:47`; C `Plans/PlanCatalog.cs:244`, `Entity/Commands/AttackCommand.cs:149`; registration `Entity/EntityModel.cs:531` | `En` 16, 91, 111, 114; hunt target 30; `Caste` (`CasteMods.GroupBonus`) 120; recent `Threaten` 119; `Colony` 130-131 | `En` (dash 94, strike 115); victim `Hp` 125; bite infection 126; fitness 129; `ColonyStats.RecordAttack` 130 |
| 5 | Flee | `FleeCommand` | Survive, Hunt | all | IsMove | 6: S `Instinct/InstinctSystem.cs:171`, `Plans/PlanCatalog.cs:167,260`, `Entity/EntityModel.Wild.cs:40`; C `Instinct/InstinctSystem.cs:249`; registration `Entity/EntityModel.cs:532` | threat source 22; nest danger field 46-55; warned state 28 | movement (zigzag legs 24-43) |
| 6 | Eat | `EatCommand` | Survive, Forage | all | none | 10: S `Instinct/InstinctSystem.cs:194`, `Plans/PlanCatalog.cs:145,176,200,278`, `Entity/EntityModel.Wild.cs:41`; C `Instinct/InstinctSystem.cs:257`, `Plans/PlanCatalog.cs:193`, `Entity/EntityModel.Cycle.cs:222`; registration `Entity/EntityModel.cs:533` | `Stomach` (`IsSated`) 18; `Carrying` 25; `NestStorage` 32, 35; food cell 30, 43; `FoodToxicity` (claim) 62; `Caste` (`CasteMods`) 34, 75 | `Carrying` 26; `NestStorage` 36; food claim 45; infection 48-50; `Stomach` 110; `Hp` (toxic) 100; PoI `EnergyEma` 80; fitness 77, 90 |
| 7 | Reproduce | `ReproduceCommand` | Social | all | EnergyGated | 5: S `Plans/PlanCatalog.cs:183,296`; C `Plans/PlanCatalog.cs:292,293`; registration `Entity/EntityModel.cs:534` | `En` 8; `Hp` 9 | `En` 13; `Hp` 15; spawn request 16; fitness 17 |
| 8 | Speech | `SpeechCommand` | Social | all | none | 2: S `Plans/PlanCatalog.cs:180`; registration `Entity/EntityModel.cs:535` | `En` 19; signal kind 48-57; food site 54; `Hp`, `En` for audio 74 | `En` 20; broadcast 23; nest alarm 51; recruitment 54, 57 |
| 9 | Mimic | `MimicCommand` | Social | all | NeedsTarget | 2: S `Plans/PlanCatalog.cs:182`; registration `Entity/EntityModel.cs:536` | nearest creature's last action 7-10 | own `MimickedAction` input 10 |
| 10 | Rest | `RestCommand` | Survive | all | none | 13: S `Plans/PlanCatalog.cs:164,253,255`, `Entity/EntityModel.Wild.cs:49`; C `Plans/PlanCatalog.cs:251,252`, `EntityManager.cs:669`, `Entity/Commands/RestCommand.cs:67`, `Entity/EntityModel.Behaviour.cs:101`, `Entity/EntityModel.Wild.cs:36`, `Entity/EntityModel.Cycle.cs:220,222`; registration `Entity/EntityModel.cs:537` | `Hp` 33, 43; `En` 34; nest nurses 36-38; at-nest 37, 40; `Colony` of neighbours 67; night 71 | `Hp` 46; `En` 47 |
| 11 | Threaten | `ThreatenCommand` | Hunt | all | NeedsTarget | 3: S `Plans/PlanCatalog.cs:159`; C `Entity/Commands/AttackCommand.cs:119`; registration `Entity/EntityModel.cs:538` | hunt target 16; distance 18-19; `En` 22 | `En` 23; facing step (movement) 28, 36, 40-52 |
| 12 | ShareFood | `ShareFoodCommand` | Social | all | NeedsTarget | 4: S `Instinct/InstinctSystem.cs:188`, `Plans/PlanCatalog.cs:182`; C `Instinct/InstinctSystem.cs:254`; registration `Entity/EntityModel.cs:539` | `Caste` 15; `En` own and receiver 18-19; `Carrying` 23; `Stomach` 24 | `Carrying` 23; `Stomach` own 24, 29 and receiver 27; `ColonyHunger` 31; gift infection 32 |
| 13 | ApproachFood | `ApproachFoodCommand` | Survive, Hunt, Forage | all | IsMove, NeedsTarget | 10: S `Instinct/InstinctSystem.cs:192`, `Plans/PlanCatalog.cs:146,152,177,201,215,281`, `Entity/EntityModel.Wild.cs:42`; C `Instinct/InstinctSystem.cs:256`; registration `Entity/EntityModel.cs:540` | food cell and distance 9-16 | movement 17 |
| 14 | ReturnNest | `ReturnNestCommand` | Survive, Forage, Social | all | IsMove | 9: S `Instinct/InstinctSystem.cs:173,179,181,183`, `Plans/PlanCatalog.cs:150,164,221`; C `Instinct/InstinctSystem.cs:251`; registration `Entity/EntityModel.cs:541` | nest position 13-17; at-nest 15 | movement 18 |
| 15 | PickUp | `PickUpCommand` | Forage | NoSoldier | NeedsTarget | 6: S `Plans/PlanCatalog.cs:151,175,214,277`; C `Plans/PlanCatalog.cs:273`; registration `Entity/EntityModel.cs:542` | `Carrying` 10; food cell 16-18; `FoodKind` (`Large` refused) 19 | food claim 22; `Carrying` 26 |
| 16 | StoreFood | `StoreFoodCommand` | Forage | NoSoldier | none | 6: S `Instinct/InstinctSystem.cs:175`, `Plans/PlanCatalog.cs:150,223`; C `Instinct/InstinctSystem.cs:250`, `Plans/PlanCatalog.cs:205`; registration `Entity/EntityModel.cs:543` | `Carrying` 7, 12; at-nest 7, 12; `Caste` (`StoreEfficiency`) 14 | `Carrying` 14; `NestStorage` 15; colony stats 17; colony memory share 19; recruitment 20 |
| 17 | Follow | `FollowCommand` | none | all | IsMove, NeedsTarget, InstinctOnly | 3: S `Instinct/InstinctSystem.cs:185`; C `Instinct/InstinctSystem.cs:253`; registration `Entity/EntityModel.cs:544` | parent position 9-17 | movement 19 |
| 18 | MoveNest | `MoveNestCommand` | none | all | PlanOnly | 3: S `Plans/PlanCatalog.cs:270`; C `Plans/PlanCatalog.cs:263`; registration `Entity/EntityModel.cs:545` | leader flag, quorum site 8-9, 14 | nest relocation (storage to corpses) 16 |
| 19 | Groom | `GroomCommand` | Social | all | NeedsTarget | 2: S `Plans/PlanCatalog.cs:181`; registration `Entity/EntityModel.cs:546` | sickest kin by `ViralLoad` 10 | patient `ViralLoad` (`Virology.Groom`) 14; contact infection 15 |

Total call sites outside `ActionCatalog.cs`: 123, counted by the grep above (registrations included).

## 2. Consolidation proposal

Fixed verbs: move to a target, carry and drop, emit a signal, transfer a resource, attack, eat, rest, idle, reproduce. "Property" means a row of `docs/audit/property_system_matrix.md`, or one of the Stage 2 perception properties (food `Toxicity` / `Size` / `Meat`, foreign `Hp` / `Energy` / `Viral`).

| # | Action | Proposal | Property the verb reads | Behaviour lost | Recoverable by property values? |
|---|---|---|---|---|---|
| 0 | Idle | keep (idle) | none | none | not applicable |
| 1 | Wander | replace with move to a target + target property | terrain height and slope (steering); `Season` / colony fields for the heading; no target means an exploratory step | Lévy step and OU heading; migration leg; Scout-only quorum vote on arrival | Lévy and migration: yes (empty target → exploratory step; migration as a direction value). Scout-only vote: **no** (keyed on `Caste`, a label). **Conflict C1.** |
| 2 | RememberPoint | merge into emit a signal | PoI `EnergyEma` (value of the signalled point) | explicit "remember here" without eating; Scout-only colony publish | memory: yes (`EnergyEma` is already credited by `Eat`, `Eat.cs:80`; visits by move). Scout-only publish: **no** (`Caste`). **Conflict C2.** |
| 3 | GoToPoint | replace with move to a target + target property | PoI `EnergyEma` (own and colony board) | choice between own and colony PoI by the direction head; recruit target | yes: target = best `EnergyEma` over both boards (same as `TryPickBest`); recruit is a target value set by a signal |
| 4 | Attack | keep (attack) | foreign `Hp`, `Energy`, `Viral` (Stage 2); `En` | none | not applicable |
| 5 | Flee | replace with move to a target + target property | `Stress`, nest danger field (negative target); foreign `Hp` / `Energy` | zigzag legs; warned speed boost | yes: legs and speed as move parameters driven by `Stress` |
| 6 | Eat | keep (eat) | food `Toxicity`, `Size`, `Meat`; `Stomach`; `NestStorage` | none | not applicable |
| 7 | Reproduce | keep (reproduce) | `En`, `Hp` | none | not applicable |
| 8 | Speech | keep (emit a signal) | `En`; signal content (four-value speech vector) | none | not applicable |
| 9 | Mimic | keep | nearest creature's last action | none | not applicable; see DEFERRED D3 (better as passive perception, outside the fixed verbs) |
| 10 | Rest | keep (rest) | `Hp`, `En`, at-nest | none | not applicable |
| 11 | Threaten | merge into emit a signal | foreign `Hp`, `Energy` (Stage 2); `En` | facing step toward the target; Threaten→Attack damage synergy (`AttackCommand.cs:119`) | facing: yes (move parameter). Synergy: yes, if it is keyed on "a threat signal was emitted recently" (signal content), not on the action index |
| 12 | ShareFood | merge into transfer a resource | receiver `En` and `Stomach`; `Carrying`; `ColonyHunger` | Nurse-only targeting of juveniles (`ShareFoodCommand.cs:15`) | hunger relief: yes (`ColonyHunger`). Nurse targeting: **no** (`Caste`). **Conflict C3.** |
| 13 | ApproachFood | replace with move to a target + target property | food `Toxicity` / `Size` / `Meat` and food distance | none (arrive-within-`EatRange` check stays) | yes |
| 14 | ReturnNest | replace with move to a target + target property | `NestStorage`, `Carrying`, at-nest (target = nest position) | none | yes |
| 15 | PickUp | merge into carry and drop | `Carrying`; food `Size` (large food not carried) | Soldier ban (`NoSoldier`) | `Size` rule: yes. Soldier ban: **no** (`Caste`). **Conflict C4.** |
| 16 | StoreFood | merge into carry and drop | `Carrying`, `NestStorage`, at-nest | caste store efficiency (`CasteMods.StoreEfficiency`); Soldier ban; colony memory share and recruitment on deposit | memory share and recruitment: yes (trigger on "drop at nest"). Efficiency and ban: **no** (`Caste`). **Conflict C5.** |
| 17 | Follow | replace with move to a target + target property | `Colony` (kin) and distance to the nearest kin | tethering to the specific parent | **no**: "parent" is a relation, not a matrix property. **Conflict C6.** |
| 18 | MoveNest | keep | leader flag, quorum site | none | not applicable; leader role is out of scope (D4) |
| 19 | Groom | merge into transfer a resource | patient `ViralLoad` | none (contact infection stays a side effect of contact) | yes |

Resulting set: idle, move to a target, carry and drop, emit a signal, transfer a resource, attack, eat, rest, reproduce, plus kept `Mimic` and `MoveNest`. That is 11 entries instead of 20. The RL subset (excluding plan-only `MoveNest`) becomes 10 actions instead of 18.

"Move to a target" absorbs six actions, so it needs a target-selection parameter. The exec heads already output a direction choice (`DirectionKind`, `Plans/PlanCatalog.cs:17-29`: Food, NestFood, OwnPoi, ColonyPoi, Frontier, Migration, Flock, Levy). It has no Nest, Threat or Kin entry. Adding them changes `DirectionOutputs` and therefore the exec output size.

### Conflicts with PHILOSOPHY

Each behaviour below is keyed on a label (`Caste`, or a parent relation), not on a property value. Merging the action loses it unless the label is replaced. For each conflict, the variant that complies with PHILOSOPHY drops the label and lets the behaviour emerge from property values and learning.

- **C1** Scout-only quorum vote (`WanderCommand.cs:128-135`). Variant: any creature may vote on arrival when the nest danger field at the site is below `QuorumMaxDanger`.
- **C2** Scout-only colony PoI publish (`RememberPointCommand.cs:11-12`). Variant: every signal carries the point's `EnergyEma`; the colony board weighs offers by value, not by caste.
- **C3** Nurse-only feeding of juveniles (`ShareFoodCommand.cs:15`). Variant: the receiver is chosen by lowest receiver `En` / `Stomach` within reach; juveniles win by need, not by the giver's caste.
- **C4** Soldier ban on pick-up (`ActionCatalog.cs:62`). Variant: no ban; carrying costs speed (`CarrySpeedMul`), so soldiers that carry are slower defenders and selection can sort it out.
- **C5** Caste store efficiency and Soldier ban on storing (`ActionCatalog.cs:63`, `StoreFoodCommand.cs:14`). Variant: one efficiency for all; the ban is dropped as in C4.
- **C6** Tether to the specific parent (`FollowCommand.cs:9-17`). Variant: the target is the nearest kin of the same `Colony`, weighted by distance. Losing the parent tether is accepted, or "parent" is added as a perceived relation in a later stage.

## 3. Impact list

### 3.1 Files referencing `EntityAction`, `ActionCatalog.Count`, `ActionCount` or `InputVectorizer.TraceOffset`

Command: `grep -rlE "EntityAction|ActionCatalog\.Count|ActionCount|TraceOffset" --include=*.cs Assets`, counted per file with `grep -cE` (lines that match).

Real dependencies on the action set:

| References | File |
|---|---|
| 52 | `Assets/Scripts/EntitySystem/AI/Plans/PlanCatalog.cs` |
| 27 | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.cs` |
| 23 | `Assets/Scripts/EntitySystem/AI/ActionCatalog.cs` |
| 21 | `Assets/Scripts/EntitySystem/AI/Instinct/InstinctSystem.cs` |
| 12 | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.Wild.cs` |
| 7 | `Assets/Scripts/EntitySystem/AI/InputVectorizer.cs` |
| 2 | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.Cycle.cs` |
| 2 | `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.Behaviour.cs` |
| 2 | `Assets/Scripts/EntitySystem/AI/Entity/Commands/AttackCommand.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/Plans/PlanRunner.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/EntityManager.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/EntityAction.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/Entity/EntityView.cs` (action names for the label, `Enum.GetNames`) |
| 1 | `Assets/Scripts/EntitySystem/AI/Entity/Commands/RestCommand.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/Colony/ColonyStats.cs` |
| 1 | `Assets/Scripts/EntitySystem/AI/SharedAI/SharedBrainSnapshot.cs` (error message prints `ActionCatalog.Count`) |
| 2 | `Assets/Tests/EditMode/BrainArchitectureVersionTests.cs` |

Name matches that are **not** the entity action set:

| References | File | Why |
|---|---|---|
| 10 | `Assets/Scripts/EntitySystem/AI/SharedAI/PerceptronLayout.cs` | `ActionCount` of one perceptron head, derived from its output size (`PerceptronLayout.cs:55`); it follows the subset sizes indirectly |
| 5 | `Assets/Scripts/EntitySystem/AI/SharedAI/PerceptronContext.cs` | same layout field |
| 5 | `Assets/Scripts/EntitySystem/AI/SharedAI/KernelLayout.cs` | same layout field; per-head cap `MaxActions = 16` (`KernelLayout.cs:6`, checked at 45) |
| 3 | `Assets/Scripts/EntitySystem/AI/SharedAI/NeuralKernels.cs` | same layout field |
| 2 | `Assets/Scripts/EntitySystem/AI/DelayedPerceptron/DelayedPerceptron.cs` | same layout field |
| 4 | `Assets/Scripts/EntitySystem/Virology/ProteinTable.cs` | `ProteinTable.ActionCount = 47`, viral protein actions |
| 3 | `Assets/Scripts/EntitySystem/Virology/VirologyRuntime.cs` | protein actions |
| 3 | `Assets/Scripts/EntitySystem/Virology/StrainCodonTable.cs` | protein actions |
| 2 | `Assets/Scripts/EntitySystem/Virology/VirologyComponent.cs` | protein actions |
| 2 | `Assets/Scripts/EntitySystem/Virology/TranslationTable.cs` | protein actions |
| 2 | `Assets/Scripts/EntitySystem/Virology/StrainComposer.cs` | protein actions |
| 1 | `Assets/Scripts/EntitySystem/Virology/CodonEmbedding.cs` | protein actions |
| 1 | `Assets/Scripts/Editor/StrainCodonTableBuilder.cs` | protein actions |

Indirect dependencies, found outside the four patterns while reading:

- `SharedAI/ActionDurationTable.cs:3-15`: two positional arrays of 20 durations. Any reorder or removal shifts every value. The durations are also simulation constants hard-coded outside `SimulationRules`.
- `SharedAI/SharedHierarchicalBrain.cs`: subsets from `ActionCatalog.BuildSubsets`; exec output sizes follow the subset sizes.
- Masks over action indices in an `int`: `ActionCatalog.cs:73-84` (`CasteActionMasks`), `PlanCatalog.Bit(...)` (`Plans/PlanCatalog.cs:135`), `EntityBrainContext.CasteMask` (`SharedAI/EntityBrainContext.cs:18`). These limit the set to 32 actions.
- `InstinctBits.NoReflex = byte.MaxValue` (`Instinct/InstinctSystem.cs:21`): reflex actions are stored as `byte`.

### 3.2 Tests

- `Assets/Tests/EditMode/BrainArchitectureVersionTests.cs` depends on the action set. It records `ExpectedActionCount = 20` (line 11, asserted at line 50), `ExpectedSubsetSizes = { 8, 5, 8, 7 }` (line 29, asserted at line 61), `ExpectedExecAux` (line 30) and exec layer sizes (lines 21-26), plus `ExpectedInputSize` (line 8), which includes the 20-value action trace. It is not modified here. It already fails after Stage 2: `SharedBrainSnapshot.Version` is 8 and the input size is 107, while the test still expects 7 and 105.
- `Assets/Tests/EditMode/SnapshotVersionTests.cs` uses `InputVectorizer.VectorSize` and `SharedBrainSnapshot.Version` symbolically. It does not hard-code the action count.
- No other test under `Assets/Tests/` references `EntityAction`, `ActionCatalog` or `ActionCount`.

### 3.3 Saved-brain consequence

Any change to the number of actions changes `InputVectorizer.ActionCount` (`InputVectorizer.cs:54`), so the trace block at `TraceOffset = 8` and every later offset move, and `VectorSize` changes. It also changes the exec subset sizes, so the exec output layers change. Either change requires incrementing `SharedBrainSnapshot.Version` (currently 8, `SharedAI/SharedBrainSnapshot.cs:11`). Every saved brain becomes incompatible: loading refuses it (`SharedAI/SharedBrainSnapshot.cs:62`) and training starts from scratch. Reordering actions without changing the count keeps the sizes, but it silently remaps trained outputs and the positional duration table. It must also bump the version.

## DEFERRED

- **D1** The mapping above is a proposal; the author decides. Implementation is a separate stage.
- **D2** Stage 2 (food traits, foreign vitals) is already on this branch (`66ba120`). Merging the movement verbs additionally needs Nest, Threat and Kin targets in `DirectionKind`, which changes the exec output size.
- **D3** `Mimic` fits none of the fixed verbs. Recommended: drop it as an action and make the observed action a permanent perception input (the `MimickedAction` input slot already exists, `InputVectorizer.cs:156-158`).
- **D4** `MoveNest` and `IsLeader` keep a leader role. Whether leadership should emerge from properties is out of scope for this stage.
- **D5** `ActionDurationTable` constants live in code, not in `SimulationRules`. They should move there before or during the consolidation.
- **D6** No usage numbers: `ColonyStats.ActionsStarted` is never persisted. A run with a dump of these counters is needed before choosing which merges matter most.
