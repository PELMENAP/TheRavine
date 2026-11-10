# TheRavine — rules for AI contributors

Read before any task: [PHILOSOPHY.md](PHILOSOPHY.md), [docs/OPTIMIZATION.md](docs/OPTIMIZATION.md) (hot paths), [docs/SECURITY.md](docs/SECURITY.md) (RPCs, loaders, the Rive interpreter, external input), [docs/audit/security_audit.md](docs/audit/security_audit.md) (open findings). Task prompts follow [docs/PROMPT_TEMPLATE.md](docs/PROMPT_TEMPLATE.md).

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
