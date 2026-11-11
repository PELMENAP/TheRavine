# TheRavine

Open-world sandbox game developed in Unity.

## Philosophy

The Ravine is a virtual place where humans and AI live and coexist under the same conditions. The player is an equally valued creature, like any other; the world is an A-Life-style ecosystem, not a script built around the player.

- **Absolute freedom:** the world can be changed by the player and by itself; any creature may change the code and the rules of the game.
- **Equality:** all creatures and players are under the same conditions and must adapt and interact.
- **AI as a player:** not only humans but also artificial intelligence can play in The Ravine.
- **Image over volume:** 3D models are in most cases redundant: the eye sees a 2D projection on the monitor and completes the volume itself; the view is available from four sides (±Z, ±X) and flat objects turn toward the camera.
- **Performance is a condition of freedom:** many living creatures are possible only with cheap simulation.

Full text: [PHILOSOPHY.md](PHILOSOPHY.md).

## Unique Systems
- Natural Artificial Life
- Procedural audio synthesis
- Local LLM item descriptions
- Rive scripting language
- Wave Function Collapse
- Infinite world streaming
- SharedHierarchicalBrain
- Gesture recognition
- Dynamic ecosystem

## Performance
- Burst Jobs
- Object Pooling
- Zero-allocation collections
- MemoryPack serialization
- Chunk streaming
- Spatial hashing
- Native Collections
- Reactive event system
- Optimization principles and techniques: [docs/OPTIMIZATION.md](docs/OPTIMIZATION.md) (AI contributors: read it before changing hot paths)

## Security
- Server-authoritative world: clients send intent, not results
- Equality as a security property: no creature, host included, gets hidden powers
- Budgeted sandbox for Rive scripts
- Security principles and techniques: [docs/SECURITY.md](docs/SECURITY.md) (AI contributors: read it before changing RPCs, loaders, the Rive interpreter or anything that accepts external input); current gaps: [docs/audit/security_audit.md](docs/audit/security_audit.md)

## Stack
- **Unity 6000.3** (3D, URP), **C#**, **Netcode for GameObjects** (host/server/client)
- **UniTask** — async/await everywhere instead of coroutines
- **R3** — reactive properties, observables (an analogue of UniRx)
- **LitMotion** — tweens and animations
- **MemoryPack** — binary serialization for saves
- **ZString / Cysharp.Text** — zero-allocation string building
- **ZLinq** — zero-allocation LINQ
- **Unity.Jobs + Burst** — heavy computations
- **LLMUnity** — local LLM for item descriptions
- **NaughtyAttributes** — editor utilities

## Opening the project

Open the repository root in the Unity Editor of the version in `ProjectSettings/ProjectVersion.txt`.
Own code is in `Assets/Scripts`; EditMode tests are in `Assets/Tests/EditMode` (Test Runner window).

## Documentation

| Document | Content |
|---|---|
| [PHILOSOPHY.md](PHILOSOPHY.md) | What the world is and what it must not become |
| [CLAUDE.md](CLAUDE.md) | Rules for AI contributors, routing table, search rules |
| [docs/map/README.md](docs/map/README.md) | Architecture maps, one per subsystem; generated code index |
| [docs/map/HOT_PATHS.md](docs/map/HOT_PATHS.md) | Tick order and the code under the zero-allocation rule |
| [docs/OPTIMIZATION.md](docs/OPTIMIZATION.md) | Optimization principles, catalog and procedure |
| [docs/SECURITY.md](docs/SECURITY.md) | Security principles, RPC registry, catalog and procedure |
| [docs/audit/security_audit.md](docs/audit/security_audit.md) | Security findings and their status |
| [docs/simulation_log.md](docs/simulation_log.md) | Columns of the simulation log |
| [docs/PROMPT_TEMPLATE.md](docs/PROMPT_TEMPLATE.md) | Template of a task prompt |

The architecture is described only in `docs/map`. This file does not repeat it.

## Checks

- `python3 tools/docmap.py gen` regenerates `docs/map/INDEX.md` and `docs/map/LARGE_FILES.md`.
- `python3 tools/docmap.py check` verifies that every path and code name in the documentation exists.

## License

[LICENSE](LICENSE)
