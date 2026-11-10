# <Stage name: what we change and in which area>

Filling rules (delete before sending):
- Text in <angle brackets> is replaced with the content of the specific task.
- The blocks LANGUAGE, PHILOSOPHY, PROJECT CONSTRAINTS, VERIFICATION and RESPONSE FORMAT are permanent: copy them without changes.
- Blocks in [square brackets] are included only if the task touches them.
- The number of items in TASK is as many as needed; each item is self-contained: files, actions, and a readiness criterion.
- The instructions and items of the prompt are written in English. The explanation to the user after the work is written in Russian.

## LANGUAGE
This prompt, its instructions and its task items are written in English. Follow them in English. After the work is done, write your explanation to the user in Russian (see RESPONSE FORMAT).

## PHILOSOPHY
- The player is one of the creatures: a rule that works for the player works for all creatures, and vice versa. Do not introduce hidden advantages or special paths for the player.
- Rules are data that can be changed: parameters live in SimulationRules / ScriptableObject, not in code.
- Do not script what can emerge by itself: do not add scripted roles or scenarios if the behaviour can come from adaptation and learning.
- Liveliness and scale matter more than detail: nothing should limit the amount of life. Priority of decisions: performance → correctness → extensibility.
- Appearance does not change the essence of the world.

If a task item contradicts the philosophy, do not perform it, describe the conflict in DEFERRED and propose a variant that complies with it.

## CONTEXT

Project: Unity 6, C#, an A-Life simulation with a hierarchical actor-critic, a genetic layer and virology; Netcode for the player.

Task: <1–3 sentences: what to do and why>.

Known facts about the problem (facts from the code, with files and lines; line numbers are approximate, verify against the file):
1. <symptom> — <file:line>, <cause in the code>.
2. <...>

[File map — list only the affected areas]
<Folder/>
  <File.cs>   <role in one line>

## TASK

One stage. Perform the items strictly in order. After each item, run the check from VERIFICATION.

If an item lacks the content of a file, request that specific file (path) and stop at that item without guessing its content. Perform the other items.

### Item 1. <Short name>
Files: <paths>.
- <What to change, concretely: signatures, behaviour, where values come from>.
- <What NOT to change>.
- Done when: <observable result>.

### Item 2. <...>
Files: <...>.
- <...>

## PROJECT CONSTRAINTS

Priority: performance -> correctness -> extensibility.
Determinism is not required: RavineRandom, parallel reductions with floating-point order, FloatMode.Fast are allowed; do not propagate seeds.
Libraries: UniTask, R3 (reactive code only where it already exists; do not add new reactive code), LitMotion.
No #region, no XML summaries, no comments in code. Style must match the surrounding code.
Zero allocation on hot paths: agent tick, Decide/ForwardPass/Train, perception, Ribosome.Step, movement, instincts, colony blackboard. LINQ, new[], ToArray(), List and closures are forbidden in the tick.
Unity.Mathematics instead of Mathf in new hot-path code.
NativeArray/NativeList: explicit Dispose and IsCreated checks.
Mass per-agent computations (instincts, boids, colony fields): SoA NativeArray indexed by ManagerIndex + Burst job; keep indices synchronised on swap-back removal.
New numeric constants of agent simulation — only in SimulationRules (plus a RulesFrame field if read on a hot path), never hard-coded. Player tuning and UI — [SerializeField] next to existing fields; name such an exception in the report.
Simulation time — only SimulationClock. Time.time, Time.deltaTime and UniTask.Delay(ms) are forbidden in agent logic. For player input and physics, Time.unscaledTime and Time.deltaTime are allowed if the file already uses them.
Do not create a PR. Do not multiply commits — only when meaningful; session trailers follow session settings; do not write the model identifier in commit messages.
Tests (EditMode/PlayMode, Assets/Tests/) may be written and modified. Do not delete, skip or weaken an existing test to make it pass; change a test only together with the behaviour it checks. State in the report whether tests were run.
[If the task changes the size of network inputs or outputs] Any such change -> increment SharedBrainSnapshot.Version and state explicitly that saved brains are incompatible.
[If the task touches network code] The server is the source of truth: validate client requests on the server.
[If the task removes code] Before removing anything, grep the name across Assets and the GUID from .meta across .prefab/.unity/.asset files. Delete a script together with its .meta. Do not hand-edit asset YAML.

## VERIFICATION

- Verify the changes by reading; state explicitly in the report that compilation was not checked.
- Re-read your own diff: check for allocations on hot paths, comments, hard-coded simulation constants, and special paths for the player (see PHILOSOPHY).
- Do not claim something is "verified" if the verification was not performed.

## RESPONSE FORMAT

Your working output follows the English instructions above. The final explanation to the user is written in Russian. No introductions and no closing phrases.
1. For each item: what was changed (files), what was verified, what was not verified.
2. Expected effect of each item — one line.
3. Push to branch <branch name>: `git push -u origin <branch name>`. On a network error, retry with waits of 2, 4, 8, 16 seconds.

## DEFERRED
<Noticed but not part of the current item or stage: decisions that require the author; conflicts with the philosophy; side problems found. One line per element.>