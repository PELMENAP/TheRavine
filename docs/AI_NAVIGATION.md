# AI Navigation Audit and Improvement Proposal

Snapshot: 2026-11-11. Scope: how fast an AI contributor (or a new human) can find the right code in this repository, how much context it burns doing so, and what to change so that the cost stays flat as the project grows.

Nothing in this file is implemented yet. Every item is a proposal.

## 1. Measured state

| Fact | Value |
|------|-------|
| Own code, `Assets/Scripts` | 497 `.cs` files, 66 582 lines |
| Share of `EntitySystem/` | 215 files, 32 570 lines (49 %); `EntitySystem/AI/` alone is 127 files, 21 474 lines |
| Third-party `.cs` inside `Assets/` | about 1 200 files, about 103 000 lines (`Plugins/UniTask`, `Packages/LitMotion`, `LLMUnity`, `Packages/Graphy`, ...) |
| Files over 800 lines | 9 (two are not ours: `FastNoiseLite.cs` 2 501, generated `GameInput.cs` 2 105) |
| Largest own files | `MapGenerator.cs` 1 681, `SimulationRules.cs` 1 301, `ChunkFoodIndex.cs` 1 052, `SharedHierarchicalBrain.cs` 917, `PlayerController.cs` 902, `EntityManager.cs` 883, `VirologyComponent.cs` 849 |
| Files over 500 lines | 17 |
| Files of 150 lines or fewer | 372 of 497 |
| Files without a namespace | 309 of 497 |
| Type declarations whose name differs from the file name | about 45 % (nested types included) |
| Assembly definitions | 14, all references are GUIDs; `Terminal/`, `GameSceneManagerSystem/`, `GlobalSystems/`, `Test/`, `Editor/` fall into `Assembly-CSharp` |
| EditMode tests | 38 files in `Assets/Tests/EditMode` |
| `.meta` files in the index | 2 240 of 4 277 tracked files |
| Agent instruction files | `CLAUDE.md` (48 lines, 4.6 KB, rules only) and `docs/PROMPT_TEMPLATE.md`; no `AGENTS.md`, no nested rule files |
| Reading that `CLAUDE.md` makes mandatory before any task | `PHILOSOPHY.md` + `docs/OPTIMIZATION.md` + `docs/SECURITY.md` + `docs/audit/security_audit.md` = 83.6 KB, roughly 20 k tokens |
| CI (`.github/`) | none |

## 2. Do the existing documents help an AI?

Yes, but at a higher price than necessary, and with a correctness risk.

### What works

- `CLAUDE.md` is loaded into every session and carries the code rules, the verification steps and the report format. Its rules are concrete and checkable.
- `README.md` names real types together with their paths. That replaces most exploratory searches.
- `docs/OPTIMIZATION.md` and `docs/SECURITY.md` end with a "Procedure for the AI" and a symptom → technique catalog. This is the most useful format in the repository: a decision procedure, not prose.
- `docs/audit/*` and `docs/simulation_log.md` are narrow reference documents that are read only when the task needs them.
- The earlier god-class split (`EntityModel.*.cs`, `SimulationRules.*.cs`, one file per agent command) directly reduces read cost.

### What costs tokens or misleads

1. **`CLAUDE.md` has rules but no map.** It does not say where anything is, what not to read, or how to search. Navigation still starts from the 37.8 KB README or from blind `grep`.
   **Its first line is the most expensive instruction in the repository.** "Read before any task" forces four documents (83.6 KB) into context for a one-line UI fix. Three of them are conditional by their own description (hot paths; RPCs and loaders; open findings), and `CLAUDE.md` already restates the philosophy and the constraints.
   **The same rules exist in three places**: `CLAUDE.md`, the permanent blocks of `docs/PROMPT_TEMPLATE.md` (copied into every task prompt), and the procedures in `OPTIMIZATION.md`. A session that uses the template pays for them twice, and three copies will drift.
2. **The README is a monolith.** 416 lines, 37.8 KB (roughly 10 k tokens), most of it in single lines of 500 to 1 500 characters. A task about the inventory pays for the boids job chain and the camera shader globals. Long single-line paragraphs also make `grep` hits and partial reads useless.
3. **Two diverged copies.** `README.md` (English) and `Assets/Scripts/README.md` (Russian, 48.6 KB) are the same document. The English one has the "Camera: Four Viewing Axes" section, the Russian one does not; the Russian one has "World random ticks — `WorldGeneration/Ticks/`", the English one does not. An agent that opens the wrong one, or both, pays twice and gets two versions of the truth.
4. **Stale facts.** Verified against the tree:
   - `AI/AISpeech/` does not exist; the folder is `EntitySystem/AI/Speech/`.
   - `WaterSystem/` does not exist under `ObjectSystem/Map/WorldGeneration/`.
   - The asmdef listed as `BehavioursMap` is actually named `B`.
   - "491 `.cs` files" (now 497).
   - Line counts in "Known Issues" item 10: `EntityManager` 733 (now 883), `SimulationRules` 1 111 (now 1 301).
5. **The biggest area is the least mapped.** These folders are never referenced as paths in the README: `AI/Colony`, `AI/Plans`, `AI/Nest`, `AI/Instinct`, `AI/Flock`, `AI/Hazards`, `AI/Utility`, `AI/Caste`, `AI/Perception`, `AI/Rules`, `AI/Debug`, `EntitySystem/Virology` (16 files, 2 511 lines), `WorldGeneration/Ticks`, `WorldGeneration/Rendering`, and `Assets/Tests`.
6. **Paths are relative to different roots.** `AI/EntityManager.cs`, `Core/DayCycle.cs`, `Shaders/Boids/BoidsStep.compute`, `Player/`, `Mob/`, `States/` cannot be opened as written; each one costs a search first.
7. **History is mixed into the map.** "Known Issues" is mostly struck-through closed items and "was replaced by" notes. That is changelog content; `git log` already holds it.
8. **Hot paths are not marked in the map.** The README does not say which files are in the agent tick, so the zero-allocation rules cannot be applied without first reading the tick driver.

## 3. Structural friction in the code itself

1. **Type name ≠ file name.** Up to 45 % of declarations cannot be found by file glob. Examples: `ChunkData`, `ObjectInstInfo` and `StructureSpawnPoint` live in `MapGenerator.cs`; `RiveHelpStructs.cs` holds 26 types. Each lookup becomes a content search.
2. **No namespaces in 62 % of files**, and the existing namespaces do not follow folders (`TheRavine.Base` 53 files, `TheRavine.Generator` 37, `TheRavine.EntityControl` 30, plus a stray `EndlessGenerators`). A `using` line does not tell where a type lives.
3. **Assembly boundaries are opaque.** GUID references cannot be read without resolving `.meta` files, one asmdef is named `B`, and five top-level folders have no asmdef, so the dependency direction cannot be checked from text.
4. **Third-party and generated code is inside the search root.** A plain `grep` over `Assets/` scans 103 k foreign lines; `Assets/Scripts` itself holds `FastNoiseLite.cs` and the generated `GameInput.cs`, 4 606 lines that should never be read.
5. **Names that defeat search.** `Entityes/`, `MusicSoudns/`, `Extentions`, `Dence3DGrid`, `JoistickController` do not match correctly spelled queries. `Model/Actions&Events/` and `Packages/Graphy - Ultimate Stats Monitor/` need shell quoting. Two different `ICommand` interfaces share one name.
6. **Large files without a declared layout.** `MapGenerator.cs` mixes the generator, `ChunkData` (starts at line 1 191) and two structs; reading it whole costs about 20 k tokens.
7. **No machine-checkable gate.** Without CI, an agent cannot confirm that a change compiles or that the EditMode tests pass, so it re-reads code to compensate.

## 4. Proposals, ordered by return on effort

### P1. Turn `CLAUDE.md` from "rules" into "rules + router", keep it under 80 lines

Keep the existing rule sections. Change three things.

**a. Replace the unconditional reading list with a conditional one.**

```markdown
Read only what the task touches:
| The change touches | Read |
|--------------------|------|
| A hot path (list: docs/map/HOT_PATHS.md) | docs/OPTIMIZATION.md |
| RPCs, loaders, Rive, any external input | docs/SECURITY.md, then the open rows of docs/audit/security_audit.md |
| A design decision (new mechanic, new rule) | PHILOSOPHY.md |
The Philosophy and Project constraints sections below are sufficient for everything else.
```

**b. Add a routing table.**

```markdown
## Where to look
Own code: `Assets/Scripts`. Tests: `Assets/Tests/EditMode`.
| Task | Read first |
|------|-----------|
| Agent AI, colonies, learning | docs/map/ai.md |
| Virology | docs/map/virology.md |
| World, chunks, NAL, ticks, camera rings | docs/map/world.md |
| Player, netcode, server authority | docs/map/player-net.md |
| Boids, motion jobs | docs/map/jobs.md |
| Saves, settings, services, timers, input | docs/map/core.md |
| Inventory, craft, UI | docs/map/inventory-ui.md |
| Terminal, Rive | docs/map/terminal.md |
```

**c. Add search rules and a documentation contract.**

```markdown
## Search
- Search `Assets/Scripts` and `Assets/Tests` only.
- Never read: `Assets/Plugins`, `Assets/Packages`, `Assets/LLMUnity`, `*.meta`,
  `Core/NewInputSystem/GameInput.cs` (generated), `ObjectSystem/Map/Tools/FastNoiseLite.cs` (vendored).
- A type is not always in a file of the same name: `grep -rnE "(class|struct|interface|enum) Name\b" Assets/Scripts`.
- Files over 500 lines: take the line range from docs/map/LARGE_FILES.md, do not read the whole file.

## Documentation contract
- A change that moves, renames, adds or deletes a file listed in docs/map updates that map in the same commit.
```

**d. Remove the duplication with `docs/PROMPT_TEMPLATE.md`.** In an environment that loads `CLAUDE.md`, the permanent blocks (LANGUAGE, PHILOSOPHY, PROJECT CONSTRAINTS, VERIFICATION, RESPONSE FORMAT) are already in context. Reduce the template to the variable blocks (CONTEXT, file map, TASK) plus one line: "Rules: CLAUDE.md". Keep a full self-contained variant only for tools that do not load `CLAUDE.md`, and generate it from `CLAUDE.md` rather than maintaining it by hand.

Add `AGENTS.md` containing one line that points to `CLAUDE.md`, for tools that look for that name.

### P2. Split the README into a short human README and per-subsystem maps

- `README.md`: what the game is, philosophy summary, stack, how to open and run, links. Target: under 80 lines.
- `docs/map/<subsystem>.md`: one file per row of the routing table above, 60 to 150 lines each. The current README sections move there almost unchanged.
- Fixed template for every map file, so that an agent can read only the part it needs:

```markdown
# <Subsystem>
Root: `Assets/Scripts/...`   Assembly: `...`   Tests: `Assets/Tests/EditMode/...`

## Entry points
| Type | File | Role (one line) |

## Data flow
3–7 numbered steps.

## Invariants
Things that break silently if violated.

## Hot paths
Files and methods under the zero-allocation rule.

## Open issues
Only open ones.
```

- One line per fact, lines under 200 characters. A `grep` hit then returns one usable fact instead of a 1 500-character paragraph.
- Every path is written from the repository root.
- Delete `Assets/Scripts/README.md` or replace it with a one-line pointer. One language (English) for agent-facing documents; a Russian human README, if wanted, should carry philosophy and overview only, with no file paths that can go stale.
- Move closed "Known Issues" out; keep open items (3, 11, the `CommandScheduler._isProcessing` part of 5) in the matching map file.

Effect: a typical task loads `CLAUDE.md` (about 1 k tokens) and one map (1 to 2 k tokens) instead of 10 k (30 k with today's mandatory reading list), and the map is more complete for that subsystem than today's README section.

### P3. Add `docs/map/HOT_PATHS.md`

A flat list of `file → methods` that run in the agent tick, in decision/forward/train, perception, `Ribosome.Step`, movement, instincts, the colony blackboard, and the Burst jobs (25 files use `BurstCompile` today). The frame order from the "Tick Driver" section belongs here. This turns "is this code hot?" from an investigation into a lookup, which is step 1 of the `OPTIMIZATION.md` procedure.

### P4. Add `docs/map/LARGE_FILES.md`, generated

For each own file over 500 lines (17 today): the member outline with line ranges. Example row: `MapGenerator.cs — 24..1190 MapGenerator, 1191..1635 ChunkData, 1636..1652 StructureSpawnPoint, 1653.. ObjectInstInfo`. Generate it with a small editor script or shell script so it is never written by hand. An agent then reads 100 lines of a 1 681-line file.

### P5. Generate the mechanical facts, hand-write only the non-derivable ones

A script (`tools/gen_map.sh` or an Editor menu item) that writes `docs/map/INDEX.md`:

- folder → file count, line count;
- type → file, for every type whose name differs from its file name;
- asmdef name → resolved reference names (GUIDs resolved through `.meta`);
- the large-file outlines from P4.

Counts, line numbers and folder names then cannot go stale, and the hand-written maps keep only what code cannot say: invariants, data flow, reasons. Remove every hand-typed count from the documents (file counts, "733 lines").

### P6. Verify documentation paths automatically

A check that extracts every backticked path from `docs/**/*.md`, `README.md`, `CLAUDE.md` and fails if it does not resolve from the repository root. Run it as a pre-commit hook or CI step. The same check run today finds `AI/AISpeech/`, `WaterSystem/`, and about 50 paths that resolve only after a search.

### P7. Keep foreign code out of the search root

- Prefer UPM (`Packages/manifest.json`, git URL or OpenUPM) for UniTask, LitMotion, Graphy and LLMUnity instead of copies under `Assets/`. This removes about 103 k lines from every default search and about 30 MB from the checkout.
- If they must stay embedded, gather them under one folder, for example `Assets/ThirdParty/`, and name it in `CLAUDE.md`.
- Move `FastNoiseLite.cs` to that folder. Move the generated `GameInput.cs` to a `Generated/` folder.
- Add a root `.ignore` (read by ripgrep-based tools) listing `*.meta`, the third-party folders, `*.prefab`, `*.unity`, `*.asset`, `*.mat`, `*.anim`.

### P8. Structural rules for new code (apply on touch, no mass refactor)

1. One top-level type per file, file named after the type. Small private helper structs next to their only user are the exception. Start with the worst offenders: `ChunkData` out of `MapGenerator.cs`, `RiveHelpStructs.cs`.
2. Namespace mirrors the folder: `TheRavine.<TopFolder>.<SubFolder>`. New files only; do not rename existing namespaces in bulk, serialized references and `asmdef` roots depend on them.
3. Soft size limit of 500 lines for own files; over the limit, split by responsibility into `partial` files with a `Type.Role.cs` name (the pattern already used by `EntityModel` and `SimulationRules`). Next candidates: `MapGenerator` (window/LOD rings, chunk supply, placement, world-command target), `ChunkFoodIndex`, `SharedHierarchicalBrain`, `PlayerController` (client intent, server integration, world edits), `VirologyComponent`.
4. Split by tick phase, not by noun, in hot systems. A file named `EntityModel.Cycle.cs` tells an agent that it is hot before it is opened.
5. Rename the asmdef `B` to `BehavioursMap`. Give `Terminal/` and `GameSceneManagerSystem/` their own asmdefs so that dependency direction is enforced by the compiler and visible in the generated index.
6. Disambiguate the two `ICommand` interfaces (`IEntityCommand`, `ITerminalCommand`).
7. Fix misspelled folder and type names when the surrounding code is touched anyway (`Entityes`, `MusicSoudns`, `Extentions`, `Dence3DGrid`, `JoistickController`), using `git mv` together with the `.meta` file. Rename `Model/Actions&Events/` to a name without `&`.

### P9. A cheap verification loop

- A CI workflow (GameCI or a self-hosted runner) that compiles the project and runs `Assets/Tests/EditMode`. Even compile-only CI removes a whole class of defensive re-reading.
- `CLAUDE.md` already tells agents to state that compilation was not checked. With CI, that line becomes "wait for the check" and the uncertainty disappears.
- Keep the `AllocationAssert` tests as the guard for P3: each entry in `HOT_PATHS.md` should have a zero-allocation test.

### P10. Scoped rules instead of one long rule file

When the root `CLAUDE.md` approaches its 80-line budget, move subsystem-specific rules into nested files that are loaded only when an agent works in that folder: `Assets/Scripts/EntitySystem/AI/CLAUDE.md` (snapshot versioning, SoA swap-back, `SimulationClock` only), `Assets/Scripts/EntitySystem/Entityes/Player/CLAUDE.md` (server authority, rate limits). Each under 30 lines.

## 5. Scaling rules

1. Every always-loaded file has a size budget. `CLAUDE.md`: 80 lines. A map file: 150 lines. Exceeding the budget means splitting, not growing.
2. Documents describe the present. History goes to `git log`; findings and their status go to `docs/audit/`.
3. Whatever a script can derive is generated. Whatever is written by hand is an invariant, a flow, or a reason.
4. A new subsystem is not finished until it has a map file and a row in the routing table.
5. Agent-facing documents follow the `OPTIMIZATION.md` format: principle, catalog, numbered procedure.
6. One source of truth per fact; other files link to it.

## 6. Suggested order

| Step | Proposal | Effort | Effect |
|------|----------|--------|--------|
| 1 | P1 `CLAUDE.md` router, conditional reading list, template de-duplication | 1 hour | Removes about 20 k tokens of unconditional reading per task |
| 2 | Fix stale paths, delete the duplicate README | 1 hour | Removes wrong answers |
| 3 | P2 split into `docs/map/` | half a day | Per-task context drops from about 10 k to about 2–3 k tokens |
| 4 | P3 `HOT_PATHS.md` | 2 hours | Hot-path rules become applicable without investigation |
| 5 | P4–P6 generator and path check | half a day | Maps stop going stale |
| 6 | P7 `.ignore`, third-party relocation | 2 hours to 1 day | Searches stop hitting foreign code |
| 7 | P8 structural rules, P9 CI, P10 scoped rules | continuous | Cost stays flat as the code grows |
