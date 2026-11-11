# TheRavine — rules for AI contributors

Unity 6, C#. Own code: `Assets/Scripts`. Tests: `Assets/Tests/EditMode`. Task prompts follow [docs/PROMPT_TEMPLATE.md](docs/PROMPT_TEMPLATE.md).

## What to read
Read only what the task touches. The Philosophy and Project constraints sections below are sufficient for everything else.

| The change touches | Read first |
|---|---|
| Agent AI, colonies, learning, speech | [docs/map/ai.md](docs/map/ai.md) |
| Virology | [docs/map/virology.md](docs/map/virology.md) |
| World, chunks, NAL, random ticks, camera rings, rendering | [docs/map/world.md](docs/map/world.md) |
| Player, Netcode | [docs/map/player-net.md](docs/map/player-net.md) |
| Agent motion, birds, other jobs | [docs/map/jobs.md](docs/map/jobs.md) |
| Entity base classes, mobs, small modules | [docs/map/entities.md](docs/map/entities.md) |
| Lifecycle, services, saves, settings, timers, input, sound | [docs/map/core.md](docs/map/core.md) |
| Inventory, craft, UI | [docs/map/inventory-ui.md](docs/map/inventory-ui.md) |
| Terminal, Rive, gestures | [docs/map/terminal.md](docs/map/terminal.md) |
| A hot path (list: [docs/map/HOT_PATHS.md](docs/map/HOT_PATHS.md)) | [docs/OPTIMIZATION.md](docs/OPTIMIZATION.md) |
| RPCs, loaders, the Rive interpreter, external input | [docs/SECURITY.md](docs/SECURITY.md), open rows of [docs/audit/security_audit.md](docs/audit/security_audit.md) |
| A new mechanic or rule | [PHILOSOPHY.md](PHILOSOPHY.md) |

## Search
- Search `Assets/Scripts` and `Assets/Tests` only. Third-party code: `Assets/Plugins`, `Assets/Packages`, `Assets/LLMUnity`; do not read it.
- Do not read `*.meta`, `Assets/Scripts/Core/NewInputSystem/GameInput.cs` (generated), `Assets/Scripts/ObjectSystem/Map/Tools/FastNoiseLite.cs` (vendored).
- A type is often not in a file of its name: `grep -n "TypeName" docs/map/INDEX.md` gives file and line.
- A file over 500 lines: take the member's line from `docs/map/LARGE_FILES.md` and read that range, not the file.
- Assemblies and their references: `docs/map/INDEX.md`, section Assemblies.

## Documentation
- A change that moves, renames, adds or deletes a file or type named in `docs/map` updates that map in the same commit.
- After changing `.cs` files run `python3 tools/docmap.py gen`, then `python3 tools/docmap.py check`; commit the regenerated files.
- Rules for maps and structural rules for new code: [docs/map/README.md](docs/map/README.md).

## Language
Task prompts and instructions are in English. The explanation to the user after the work is in Russian.

## Philosophy
- The player is one of the creatures: a rule that works for the player works for all creatures, and vice versa. No hidden advantages or special paths for the player.
- Rules are data that can be changed: parameters live in SimulationRules / ScriptableObject, not in code.
- Do not script what can emerge by itself: no scripted roles or scenarios if the behaviour can come from adaptation and learning.
- Liveliness and scale matter more than detail: nothing should limit the amount of life.
- Appearance does not change the essence of the world.

If a task item contradicts the philosophy, do not perform it: describe the conflict in DEFERRED and propose a variant that complies with it.

## Project constraints
- Priority: performance → correctness → extensibility.
- Determinism is not required: RavineRandom, parallel reductions with floating-point order, FloatMode.Fast are allowed; do not propagate seeds.
- Libraries: UniTask, R3 (reactive code only where it already exists; do not add new reactive code), LitMotion.
- No #region, no XML summaries, no comments in code. Style must match the surrounding code.
- Zero allocation on hot paths: agent tick, Decide/ForwardPass/Train, perception, Ribosome.Step, movement, instincts, colony blackboard. LINQ, new[], ToArray(), List and closures are forbidden in the tick.
- Unity.Mathematics instead of Mathf in new hot-path code.
- NativeArray/NativeList: explicit Dispose and IsCreated checks.
- Mass per-agent computations (instincts, boids, colony fields): SoA NativeArray indexed by ManagerIndex + Burst job; keep indices synchronised on swap-back removal.
- New numeric constants of agent simulation — only in SimulationRules (plus a RulesFrame field if read on a hot path), never hard-coded. Player tuning and UI — [SerializeField] next to existing fields; name such an exception in the report.
- Simulation time — only SimulationClock. Time.time, Time.deltaTime and UniTask.Delay(ms) are forbidden in agent logic. For player input and physics, Time.unscaledTime and Time.deltaTime are allowed if the file already uses them.
- Do not create a PR unless asked. Do not multiply commits — only when meaningful; do not write the model identifier in commit messages.
- Tests (EditMode/PlayMode, Assets/Tests/) may be written and modified. Do not delete, skip or weaken an existing test to make it pass; change a test only together with the behaviour it checks. State in the report whether tests were run.
- If a change alters the size of network inputs or outputs: increment SharedBrainSnapshot.Version and state explicitly that saved brains are incompatible.
- If a change touches network code: the server is the source of truth; validate client requests on the server.
- If a change removes code: grep the name across Assets and the GUID from .meta across .prefab/.unity/.asset files first. Delete a script together with its .meta. Do not hand-edit asset YAML.

## Task procedure
- Perform task items strictly in order; after each item, run the verification below.
- If an item lacks the content of a file, request that specific file (path) and stop at that item without guessing its content. Perform the other items.

## Verification
- Verify the changes by reading; state explicitly in the report that compilation was not checked.
- Re-read your own diff: check for allocations on hot paths, comments, hard-coded simulation constants, and special paths for the player.
- Do not claim something is "verified" if the verification was not performed.

## Response format
The final explanation to the user is in Russian. No introductions and no closing phrases.
1. For each item: what was changed (files), what was verified, what was not verified.
2. Expected effect of each item — one line.
3. Push to the session's branch: `git push -u origin <branch>`. On a network error, retry with waits of 2, 4, 8, 16 seconds.
4. DEFERRED: noticed but not part of the current item or stage — decisions that require the author, conflicts with the philosophy, side problems found. One line per element.
