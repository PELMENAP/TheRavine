# <Stage name: what we change and in which area>

Filling rules (delete before sending):
- Text in <angle brackets> is replaced with the content of the specific task.
- Blocks in [square brackets] are included only if the task touches them.
- The number of items in TASK is as many as needed; each item is self-contained: files, actions, and a readiness criterion.
- The prompt is written in English. The explanation to the user after the work is written in Russian.
- The rules are not copied here. Language, philosophy, project constraints, verification and response format live in
  [CLAUDE.md](../CLAUDE.md), which Claude Code loads by itself. The single source is that file.
- For a tool that does not load `CLAUDE.md`: paste its sections from "Language" to "Response format" after TASK, unchanged.

## RULES

Follow `CLAUDE.md` of the repository: language, philosophy, project constraints, task procedure, verification, response format.
Read the map of the affected area first: <docs/map/<area>.md>.
[If the task is on a hot path] Read `docs/OPTIMIZATION.md` and follow its procedure.
[If the task touches RPCs, loaders, the Rive interpreter or external input] Read `docs/SECURITY.md` and follow its procedure.

## CONTEXT

Task: <1–3 sentences: what to do and why>.

Known facts about the problem (facts from the code, with files and lines; line numbers are approximate, verify against the file):
1. <symptom> — <file:line>, <cause in the code>.
2. <...>

[Files outside the map of the area — list only what the map does not already name]
<path>   <role in one line>

## TASK

One stage. Perform the items strictly in order.

### Item 1. <Short name>
Files: <paths>.
- <What to change, concretely: signatures, behaviour, where values come from>.
- <What NOT to change>.
- Done when: <observable result>.

### Item 2. <...>
Files: <...>.
- <...>

## BRANCH

Push to branch <branch name>.

## DEFERRED
<Noticed but not part of the current item or stage: decisions that require the author; conflicts with the philosophy; side problems found. One line per element.>
