# Project maps

One map per subsystem. Open only the map of the area the task touches; the routing table is in `CLAUDE.md`.

| File | Covers |
|---|---|
| `docs/map/core.md` | Entry point, scene lifecycle, services, saves, settings, timers, input, sound, day cycle |
| `docs/map/entities.md` | `AEntity`, components, player / bot / mob entities, mob streaming, small gameplay modules |
| `docs/map/player-net.md` | Player controller, Netcode, server authority, camera input |
| `docs/map/ai.md` | Agents, colonies, shared brain, learning, perception, speech, debug |
| `docs/map/virology.md` | Genome tape, ribosome, strains, infection |
| `docs/map/world.md` | Chunks, terrain, objects, random ticks, NAL, camera rings, rendering |
| `docs/map/jobs.md` | Agent motion, move planner, bird boids, menu boids |
| `docs/map/inventory-ui.md` | Inventory, craft, item data, UI |
| `docs/map/terminal.md` | Terminal, Rive interpreter, gestures |
| `docs/map/HOT_PATHS.md` | Files and methods under the zero-allocation rule, tick order |
| `docs/map/INDEX.md` | Generated: folders, assemblies, type → file |
| `docs/map/LARGE_FILES.md` | Generated: member outline with line numbers for files over 500 lines |

## Tools

- `python3 tools/docmap.py gen` rewrites `INDEX.md` and `LARGE_FILES.md`.
- `python3 tools/docmap.py check` fails on a path that does not exist and on a backticked name that does not occur in the code.
- `python3 tools/docmap.py check --strict` also fails when the generated files are stale.
- CI runs the check on every push: `.github/workflows/docs.yml`.

## Rules for these files

1. Size budget: `CLAUDE.md` 80 lines, a map 150 lines, a nested `CLAUDE.md` 30 lines. Over budget means split, not grow.
2. One fact per line, under 200 characters. A search hit must be usable alone.
3. Every path starts at the repository root.
4. A map describes the present. History belongs to `git log`; findings and their status to `docs/audit/`.
5. Whatever a script can derive is generated: counts, line numbers, type locations, assembly references.
6. Hand-written content is what code cannot say: entry points, data flow, invariants, reasons.
7. One source of truth per fact; other files link to it.
8. A new subsystem gets a map file and a row in the routing table of `CLAUDE.md` in the same commit.
9. A change that moves, renames, adds or deletes a file named in a map updates that map in the same commit.

## Map template

```markdown
# <Subsystem>
Root: `Assets/Scripts/...`. Assembly: `...`. Tests: `Assets/Tests/EditMode/...`.

## Entry points
| Type | File | Role |

## Data flow
Numbered steps.

## Invariants
What breaks silently if violated.

## Hot paths
Link to the section of HOT_PATHS.md, or "none".

## Open issues
Open ones only.
```

## Structural rules for new code

Applied when a file is created or touched for another reason; no mass refactor.

1. One top-level type per file, file named after the type. A small helper struct next to its only user is the exception.
2. Namespace mirrors the folder: `TheRavine.<TopFolder>.<SubFolder>`. Existing namespaces are not renamed in bulk.
3. Soft limit of 500 lines per own file. Over it, split by responsibility into partial files named `<Type>.<Role>.cs`.
4. In hot systems split by tick phase, so the file name says whether the code is hot (`EntityModel.Cycle.cs`).
5. A new top-level folder under `Assets/Scripts` gets its own asmdef.
6. A renamed or moved file is moved with `git mv` together with its `.meta`.
