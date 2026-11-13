# Brain interface v11: verb set, target directions, carried object, one version bump

## RULES

Follow `CLAUDE.md` of the repository: language, philosophy, project constraints, task procedure, verification, response format.
Read the map of the affected area first: `docs/map/ai.md`, then `Assets/Scripts/EntitySystem/AI/CLAUDE.md` and `docs/audit/action_census.md`.
Item 5 touches the world and the network: read `docs/map/world.md` and `docs/map/player-net.md` before it.
The task is on a hot path (agent tick, perception, Decide): read `docs/OPTIMIZATION.md` and follow its procedure.

## CONTEXT

Task: change the inputs and outputs of the shared brain once, to their intended long-term shape, so that later mechanics
(farming, per-cell soil, colony task board, structures) need no further change of `SharedBrainSnapshot.Version`.
The stage merges 20 actions into 11 verbs, extends the target directions, lets every creature carry one world object
through the player's edit path, and adds the matching inputs. All of it is one push with exactly one version increment, 10 → 11.

Known facts about the problem (facts from the code, with files and lines; line numbers are approximate, verify against the file):
1. Version is 10 — `Assets/Scripts/EntitySystem/AI/SharedAI/SharedBrainSnapshot.cs:11`. Input size is 124, action count 20,
   subset sizes `{ 8, 5, 8, 7 }`, direction outputs 8 — `Assets/Tests/EditMode/BrainArchitectureVersionTests.cs:7-30`.
   `docs/audit/action_census.md` was written at version 8 and is stale in places listed below.
2. Action set: `EntityAction.cs:1-8` (20 values); table, goal masks and subsets — `ActionCatalog.cs:44-116`.
   `ActionCatalog.Count` feeds the trace block of the input: `InputVectorizer.cs:57-66`, `:113-119`.
3. One exec head is capped at 16 actions — `SharedAI/KernelLayout.cs:6`, checked at `:48`.
4. Only `Survive` and `Forage` executors output a direction choice — `SharedAI/SharedHierarchicalBrain.cs:35` (`UsesDirections`),
   `:37-42` (`ExecAux`), `:591-595`. `Hunt` and `Social` output heading and curvature instead.
5. `DirectionKind` has 8 values — `Plans/PlanCatalog.cs:17-29`. The mask of available directions is a `byte`:
   `SharedAI/EntityBrainContext.cs:50`, `Entity/EntityModel.cs:108`, `Entity/EntityModel.Directions.cs:83`. Eight bits are all used.
6. Directions are computed in `Entity/EntityModel.Directions.cs:48-67` (`ComputeDirections`) and resolved in `:86-110`.
7. Plans return an action index and keep a direction in `PlanProgress.Direction` — `Plans/PlanCatalog.cs:144-190` (`EntryMask`),
   `:193-309` (`Next`); the runner passes both to the command — `Plans/PlanRunner.cs:140-153`.
8. Instinct reflexes are action bytes without a direction — `Instinct/InstinctSystem.cs:196-229`; reflex priority is a switch on the action.
9. Action durations are two positional arrays of 20 hard-coded values — `SharedAI/ActionDurationTable.cs:3-15`.
10. Carried food is a scalar `Carrying` — `Entity/EntityModel.cs:371-378`; `PickUpCommand` takes food only from `ChunkFoodIndex`
    and refuses `FoodKind.Large` — `Entity/Commands/PickUpCommand.cs:13-27`. Agents cannot hold a world object.
11. The player edits the world through `WorldEdits.TryPickUp` / `TryPlace` —
    `Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs:217-262`, called from the server RPCs
    `Assets/Scripts/EntitySystem/Entityes/Player/PlayerController.cs:758-793` with a range check (`TryResolveEditor`) and a rate
    budget (`editRate`, `editBurst`, `:33`, `:841`; `RateBudget` in `Assets/Scripts/EntitySystem/Entityes/Player/Components/RateBudget.cs`).
    The result is mirrored to the owner client by `PlaceResultClientRpc` / `PickUpResultClientRpc`.
12. Pickable means `ObjectInfo.InventoryItemInfo != null` (`WorldCommandBuffer.cs:229`). `ObjectInfo` has no weight field.
13. `Stress` is already an input — `InputVectorizer.cs:141`. Do not add it again.
14. The Soldier ban on pick-up and store (census conflicts C4, C5) is already gone: every row of `ActionCatalog.BuildTable`
    uses `AllCastes`. The Scout quorum vote (C1) is already a weight, `ScoutVoteWeight` — `Entity/Commands/WanderCommand.cs:78`.
15. Caste branches that still gate behaviour: `Entity/Commands/RememberPointCommand.cs:11` (Scout-only colony publish, C2),
    `Entity/Commands/ShareFoodCommand.cs:17` (Nurse-only juvenile target, C3).
16. The hunt target is the nearest foreign creature or player, chosen from state — `Entity/EntityModel.cs:252-271`.
17. The danger channel already has a mass centroid per colony — `Nest/NestState.cs:48-50`, `:120-122` (`ColonyFieldStats.DangerX/Y`).
18. `Mimic` only writes the `MimickedAction` input (`InputVectorizer.cs:167-169`); the census (D3) recommends making it passive perception.

Files outside the map of the area:
`Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs`   `WorldEdits`, the single world-edit path
`Assets/Scripts/ObjectSystem/Map/WorldGeneration/MapGenerator.cs`   `TryGetObject` (`:701-712`), `IsOccupiedAt` (`:747`); large file, read ranges
`Assets/Scripts/EntitySystem/Entityes/Player/PlayerController.cs`   how the server applies and replicates a player edit (`:758-841`)

Target interface (the contract of this stage):

Verbs, `EntityAction`, 11 values in this order:
`Idle, MoveTo, Attack, Eat, Reproduce, Signal, Rest, Give, Take, Put, MoveNest`.

| Verb | Absorbs | Meaning comes from |
|---|---|---|
| `MoveTo` | `Wander`, `GoToPoint`, `Flee`, `ApproachFood`, `ReturnNest`, `Follow` | the decision's `DirectionKind` |
| `Signal` | `Speech`, `Threaten`, `RememberPoint` | the goal of the decision: Social — utterance; Hunt with a valid hunt target — threat display; Forage — mark the point in own memory and offer it to the colony board |
| `Give` | `ShareFood`, `Groom` | the receiver: lowest `En` / `Stomach` kin in reach gets food; if the giver has no food to give and a kin in reach has `ViralLoad`, groom it |
| `Take` | `PickUp` | direction `Object` — the sensed world object; any other direction — food, as today |
| `Put` | `StoreFood` | carried food at the nest — store it; a held object — place it in the world |
| `Mimic` | removed as an action | the observed action of the nearest creature becomes a permanent input |

Goal subsets: Survive `{ Idle, MoveTo, Eat, Rest }`; Hunt `{ Idle, MoveTo, Attack, Signal }`;
Forage `{ MoveTo, Eat, Signal, Take, Put }`; Social `{ Idle, MoveTo, Reproduce, Signal, Give }`. `MoveNest` stays `PlanOnly`.

`DirectionKind`, 15 values: the 8 existing in their current order, then
`Nest, Escape, Kin, Danger, Object, Task, Count`.

New inputs: held-object flag (1); direction to the sensed object (2) and its proximity (1); direction to the colony danger
centroid (2); task direction (2), task proximity (1), task reward (1), task kind (1). The trace block shrinks from 20 to 11.
Expected `VectorSize`: 124 − 9 + 11 = 126; verify by the constant chain, do not assume.

## TASK

One stage. Perform the items strictly in order. `SharedBrainSnapshot.Version` is changed only in Item 7.

### Item 1. Rules for the new verbs
Files: `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules*.cs`, `Assets/Scripts/EntitySystem/AI/SharedAI/ActionDurationTable.cs`,
`Assets/Tests/EditMode/RulesDefaultsTests.cs`.
- Move minimum and maximum durations from `ActionDurationTable` into `SimulationRules` as two serialized arrays indexed by the new
  `EntityAction`, with defaults in the field initialisers. A merged verb takes the widest range of the actions it absorbs.
  `ActionDurationTable.Min/Max` keep their signatures and read the rules; the fallback values for an index outside the arrays stay.
- Add rules, each with a `RulesFrame` field where the tick reads it: object sense radius in cells, object reach (distance at which
  `Take` and `Put` act on an object), edit rate and edit burst per creature, maximum agent world edits per frame for the whole
  simulation, carry speed multiplier for a held object.
- Do not hand-edit `SimulationRules.asset`.
- Done when: no duration or new number of this stage is a literal outside `SimulationRules`; `RulesDefaultsTests` covers the new fields
  in the way it covers the existing ones.

### Item 2. Verb set and commands
Files: `Assets/Scripts/EntitySystem/AI/EntityAction.cs`, `ActionCatalog.cs`, `Entity/Commands/*`, `Entity/EntityModel.cs` (command
registration, about `:527-546`), `Entity/EntityModel.Behaviour.cs`, `Entity/EntityModel.Cycle.cs`, `EntityManager.cs`,
`Colony/ColonyStats.cs`, `Entity/EntityView.cs`, `InputVectorizer.cs` (only the mimic slot).
- Replace the enum and `ActionCatalog.BuildTable` with the 11 verbs and the goal subsets of the contract. Keep the flags
  (`IsMove`, `NeedsTarget`, `EnergyGated`, `PlanOnly`, `Communal`) with the meaning they have now; `InstinctOnly` is removed
  together with `Follow` if nothing else uses it.
- One command class per verb. `MoveToCommand` derives from `PlannedMoveCommand` and selects its behaviour by the decision's
  direction: `Levy` and `Frontier` — the exploratory step of `WanderCommand` (Lévy step, OU heading, quorum vote on arrival with
  `ScoutVoteWeight`); `Migration` — the migration leg; `OwnPoi` / `ColonyPoi` — `GoToPointCommand` including the recruit target
  and the PoI visit bookkeeping; `Food` — `ApproachFoodCommand` with the arrive-within-`EatRange` check; `Nest` —
  `ReturnNestCommand`; `Escape` — `FleeCommand` with its zigzag legs and warned speed boost; `Kin` — `FollowCommand`.
  Move the code, do not rewrite the behaviour.
- `SignalCommand`, `GiveCommand`, `PutCommand`, `TakeCommand` follow the table of the contract. In `Signal` the colony publish
  has no caste test: the board weighs the offer by the point's `EnergyEma`. In `Give` the receiver is chosen by need, with no
  caste test. `CasteMods.StoreEfficiency` stays: it is a multiplier, not a lock.
- The Threaten → Attack damage synergy (`AttackCommand.cs`, the check of a recent `Threaten`) is keyed on "a threat display was
  emitted recently", not on an action index.
- Remove `MimicCommand`. The observed action of the nearest creature is written to `VectorizerFrame.MimickedAction` on every
  perception pass, from the same source the command used.
- Delete every replaced command file together with its `.meta`; grep each removed name across `Assets` first.
- In this item `Take` handles food only and `Put` stores food only; the object branch comes in Item 5.
- Done when: `grep -rn "EntityAction\.\(Wander\|GoToPoint\|Flee\|ApproachFood\|ReturnNest\|Follow\|Speech\|Threaten\|RememberPoint\|ShareFood\|Groom\|PickUp\|StoreFood\|Mimic\)\b" Assets`
  returns only lines that Item 4 will change (`Plans/`, `Instinct/`), and no caste test remains in the commands except multipliers.

### Item 3. Target directions for every goal
Files: `Assets/Scripts/EntitySystem/AI/Plans/PlanCatalog.cs` (the enum), `Entity/EntityModel.Directions.cs`, `Entity/EntityModel.cs`,
`Entity/EntityModel.Cycle.cs`, `SharedAI/EntityBrainContext.cs`, `SharedAI/SharedHierarchicalBrain.cs`.
- Extend `DirectionKind` as in the contract. Widen the direction mask from `byte` to `ushort` everywhere it is stored or passed,
  including the `PickMasked` call.
- `ComputeDirections` fills the new kinds: `Nest` — home position (`HomePosition`, so a den counts); `Escape` — away from the
  threat source that `FleeCommand` uses today, with the nest danger field as fallback; `Kin` — the parent while the creature is
  a juvenile with a known parent, otherwise the nearest kin of the same colony; `Danger` — toward the colony danger centroid
  when `DangerMass` is above a threshold from `SimulationRules`; `Object` — set in Item 5; `Task` — never set in this stage.
  A kind without a valid target leaves its mask bit clear, as the existing kinds do.
- Every executor outputs a direction choice and a direction offset: `UsesDirections` is true for all four goals. `Hunt` and
  `Social` keep their heading, curvature, style, tempo and speech outputs unchanged; the direction block is added to them.
  Recompute the aux offsets so that no two blocks overlap.
- Do not change the coordinator outputs or `PlanCatalog.Count`.
- Done when: every `MoveTo` started by the brain carries a direction whose mask bit was set, and the exec output size of each goal
  equals `ActionSubsets[goal].Length + 1 + ExecAux(goal)` (the existing check near `SharedHierarchicalBrain.cs:864` holds).

### Item 4. Plans and instincts on the verbs
Files: `Assets/Scripts/EntitySystem/AI/Plans/PlanCatalog.cs`, `Plans/PlanRunner.cs`, `Instinct/InstinctSystem.cs`,
`Entity/EntityModel.Behaviour.cs`, `Entity/EntityModel.Cycle.cs`.
- Rewrite `EntryMask` and `Next` onto the verbs. A step that used a movement action returns `MoveTo` and sets `PlanProgress.Direction`
  to the matching kind. Comparisons with `lastAction` that distinguished movement actions compare the verb and the direction.
  The plan logic itself (phases, hops, completion and failure conditions, rewards) does not change.
- `PlanRunner.Launch` passes the destination for `MoveTo` with an exploratory direction, where it tested `Wander`.
- The instinct job writes a direction byte next to the reflex action; the reflex is started with that direction. Reflex priority
  no longer depends on the old action index: keep the same priority numbers per reflex cause.
- Do not add or remove plans or reflex causes. The Nurse leash reflex and the Worker-only `PreferPickUp` hint are not changed here.
- Done when: the grep of Item 2 returns nothing, and each plan and each reflex cause maps to the same behaviour as before
  (list the mapping in the report as a table: old action → verb + direction).

### Item 5. Held object through the single world-edit path
Files: `Assets/Scripts/EntitySystem/AI/Entity/EntityModel.cs`, `Entity/EntityModel.Cycle.cs`, `Entity/EntityModel.Directions.cs`,
`Entity/Commands/TakeCommand.cs`, `Entity/Commands/PutCommand.cs`, `Population/ColonyPopulation.cs`, `EntityManager.cs`,
`Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs` only if a shared helper is needed.
- State: `HeldObject` (prefab id, `ObjectInstInfo.PlaceholderId` when empty) and its amount, plain fields next to `Carrying`.
  No SoA array: no job reads it.
- Perception: on the cadence of the cached-food refresh, scan cells within the object sense radius through
  `MapGenerator.TryGetObject` and cache the nearest object that `WorldEdits.TryPickUp` would accept (`InventoryItemInfo != null`)
  and that is not food handled by `ChunkFoodIndex`. Zero allocation; the cell count per scan is bounded by the rule.
  The cached object sets direction `Object` when the hands are empty.
- `Take` with direction `Object`: hands empty, object within reach, per-creature edit budget and the per-frame global cap not
  exhausted → `WorldEdits.TryPickUp`; on `Ok` the object becomes `HeldObject`. Any other result fails the command.
- `Put` with a held object: `WorldEdits.TryPlace` at the cell in front of the creature within reach, same budgets; on `Ok` the
  hands are empty. With carried food at the nest `Put` stores food as before; food has priority when both are present.
- A held object slows the creature by the rule of Item 1 in the same place where `CarrySpeedMul` is applied (`EntityModel.cs:449`).
- Death, despawn and swap-back removal: try `WorldEdits.TryPlace` at the creature's cell; if it is refused the object is lost.
- Agents edit the world on the server only, and the edit must reach every client the way a player's edit does. Read how
  `PlayerController` replicates an accepted edit. If that replication is addressed to the owner client only and no path exists
  that updates the other clients, stop this item, report it, and leave `Take` / `Put` for objects failing with the inputs wired.
- The per-creature budget uses the same `RateBudget` type as the player and the same time base as agent logic (`SimulationClock`).
- No reward is added for taking or placing an object. No caste test. No test for whose object it is.
- Done when: an agent can pick up and place only what the player can, only through `WorldEdits`, and no code path changes the
  map for an agent outside `WorldEdits`.

### Item 6. Inputs
Files: `Assets/Scripts/EntitySystem/AI/InputVectorizer.cs`, `Entity/EntityModel.Cycle.cs` (where `VectorizerFrame` is filled).
- Trace block follows `ActionCatalog.Count`. The mimic slot keeps its two inputs, normalised by the new count.
- Add to the direction block: object direction, danger-centroid direction, task direction. Add to the state block: held-object flag.
  Add to the tail: object proximity, task proximity, task reward, task kind (−1 take, +1 put, 0 none).
- The task inputs are written as zero in this stage; `VectorizerFrame` gets the fields so that the future board only fills them.
- Grow the offset constants explicitly; do not rely on the zero-filled gap before `PlanEmaOffset`.
- Done when: `VectorSize` follows from the constants, every new slot is written on every call, and `Vectorize` allocates nothing.

### Item 7. Version, tests, maps
Files: `Assets/Scripts/EntitySystem/AI/SharedAI/SharedBrainSnapshot.cs`, `Assets/Tests/EditMode/BrainArchitectureVersionTests.cs`,
`Assets/Tests/EditMode/BrainAllocationTests.cs`, `docs/map/ai.md`, `docs/map/HOT_PATHS.md`, generated maps.
- `SharedBrainSnapshot.Version` 10 → 11, once.
- Update the expected constants of `BrainArchitectureVersionTests` to the new architecture: version, input size, action count,
  direction outputs, subset sizes, exec aux, coordinator and exec layer sizes. Show the arithmetic for each number in the report.
- Add an `AllocationAssert.Zero` test for the object scan and for `Vectorize` with the new inputs.
- Update `docs/map/ai.md` (verbs, directions, held object). Add one line at the top of `docs/audit/action_census.md` saying which
  commit implemented the consolidation; do not rewrite the census.
- Run `python3 tools/docmap.py gen`, then `python3 tools/docmap.py check`; commit the regenerated files.
- Done when: the version is 11, the tests' constants match the code by reading, and the report states that saved brains of
  version 10 are incompatible and training starts from scratch.

## BRANCH

Push to branch `claude/practical-mendel-c5lbxd`.

## DEFERRED
- `FeignDeath` is not added as a verb: it would be a scripted trick. The compliant variant is a perception rule for every creature (a motionless creature is detected at a shorter range), which needs no change of the brain; `Idle` then serves as feigning.
- The "possibility map" colony channel is not added: the danger channel with its centroid and the new `Danger` direction cover the search for a lost target.
- `Stress` as a new input is not needed: it exists.
- Scout-only carrying is not implemented: it is a caste lock. A caste multiplier on the held-object speed rule is the compliant variant; author decides.
- Colony task board, `ColonyCulture` rules, Rive scripts for colonies, blueprints: later stages; they fill the reserved task inputs and the `Task` direction without a version change.
- Per-cell soil, NAL growth from fertility, sapling competition, "put into soil" as fertilising: world rules, no brain change.
- Property rule (may a creature take objects placed by others): author decides; it must be one rule in `SimulationRules` for the player and agents alike.
- The player's `editRate` / `editBurst` are `[SerializeField]` in `PlayerController`, the agents' are in `SimulationRules`: two sources for one rule; unify.
- `ObjectInfo` has no weight: the held-object slowdown is one multiplier for every object until a weight field exists.
- The held object is not drawn on the agent.
- Remaining caste branches: Nurse leash reflex (`InstinctSystem.cs:211`), Worker-only `PreferPickUp` (`EntityModel.Cycle.cs:459`), Nurse exception in `EntityModel.Social.cs:67`.
- `PlanHint.PlayerNear` and the player branch of the hunt target (`EntityModel.cs:252-271`) treat the player as a separate kind of creature.
- `MoveNest` and `IsLeader` keep a leader role (census D4).
- No usage numbers for actions exist (census D6): the merge is done without knowing which old actions the brain used.
