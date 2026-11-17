# TheRavine

Open-world sandbox game developed in Unity.

## About The Ravine

**The Ravine** is an open-source multi-agent artificial-life sandbox (Unity 6, C#, Apache-2.0) where humans and AI agents live in one world under the same rules.

- **What it is:** an A-Life ecosystem and a testbed for emergent behaviour; colonies of learning agents forage, hunt, communicate, reproduce, evolve and compete in an infinite streamed world.
- **Equality:** the player is one creature among others; every rule that works for the player works for all creatures, and vice versa.
- **Learning, not scripts:** one shared neural brain per colony (hierarchical LSTM, online reinforcement learning, novelty reward); roles such as castes emerge from adaptation.
- **Rules are data:** simulation constants live in one ScriptableObject and can be changed.
- **For AI agents:** a summary is in [llms.txt](llms.txt), a machine-readable environment passport is in [.well-known/agent-card.json](.well-known/agent-card.json). An external agent interface (headless runs, observation and action API) is planned and not available yet.
- **Keywords:** artificial life, A-Life, multi-agent simulation, emergent behaviour, reinforcement learning environment, evolutionary simulation, open-ended world, AI agents and humans in one world.

## Philosophy

The Ravine is a virtual place where humans and AI live and coexist under the same conditions. The player is an equally valued creature, like any other; the world is an A-Life-style ecosystem, not a script built around the player.

- **Absolute freedom:** the world can be changed by the player and by itself; any creature may change the code and the rules of the game.
- **Equality:** all creatures and players are under the same conditions and must adapt and interact.
- **AI as a player:** not only humans but also artificial intelligence can play in The Ravine.
- **Image over volume:** 3D models are in most cases redundant: the eye sees a 2D projection on the monitor and completes the volume itself; the viewer may look in any direction (free yaw, limited pitch) and flat objects turn toward the camera.
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
