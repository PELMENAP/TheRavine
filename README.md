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


# TheRavine — Project Architecture Summary

## Layout
All scripts are in `Assets/Scripts/` (483 `.cs` files; count: `find Assets/Scripts -name '*.cs' | wc -l`). Assemblies (asmdef in `Assets/Scripts`, 14): `Audio`, `BehavioursMap`, `Core`, `DetectorSystem`, `DialogGraph`, `EntitySystem`, `EventSystem`, `Info`, `Inventory`, `Netcode`, `ObjectSystem`, `SkillSystem`, `UIInventory`, `View`.

## Stack
- **Unity 6000.3** (3D, URP), **C#**, **Netcode for GameObjects** (host/server/client)
- **UniTask** — async/await everywhere instead of coroutines
- **R3** — reactive properties, observables (an analogue of UniRx)
- **LitMotion** — tweens and animations
- **MemoryPack** — binary serialization for saves
- **ZString / Cysharp.Text** — zero-allocation string building
- **ZLinq** — zero-allocation LINQ
- **Unity.Jobs + Burst** — heavy computations
- **LLMUnity** — local LLM for item descriptions and NPC chat
- **NaughtyAttributes** — editor utilities

---

## Camera: Four Viewing Axes

The camera looks along one of four horizontal axes: +Z, −Z, +X, −X (`ViewFacing`). The view is turned in 90° steps (see "Input Control"); there is no free rotation around X or Y. The single source of the view is `ViewOrientation`, written only by the local `CameraComponent`; `CameraPlace.facing` carries the requested side.

Consequences for optimization and mechanics:
- Object visibility is a half-space along `Forward()` relative to the camera plus a lateral window along `Right()` whose width grows with the distance. The "behind the camera" test is `dot(pos − cam, Forward) < −margin`; a full frustum test is not needed.
- `MapGenerator.ViewRingOf` computes the distance to the window centre along `Forward()` and the lateral offset along `Right()` — LOD rings, the water and terrain blocks and `Facing` are aligned to this same axis.
- Objects behind the camera (and far beyond the side edge of the screen) may skip animation and full-precision simulation: when they re-enter the frame, their state must be restored without a visible jump (see `PHILOSOPHY.md`, §5 "Image Over Volume" and §3 "The World as an Ecosystem").
- The simulation, perception and rules do not depend on the view: the NAL / random-tick region is a square around the window (see "Camera View" below), and chunks behind the camera are loaded, ticked and retained like the others. Mechanics that must be noticeable to the player may still be simplified outside the camera frame, but never by reading `ViewOrientation` in agent code.

## Entry Point and Lifecycle

### `GameInitializer` (`Terminal/GameInitializer.cs`, DontDestroyOnLoad, Awake)
Creates all global services **before** the game scene loads:
- `RavineLogger` → `ServiceLocator.Services`
- `ActionMapController` (`Core/NewInputSystem/`)
- `GlobalSettingsController` (quality, grass, controls settings)
- `WorldRegistry` (list of worlds, current world)
- `AutosaveCoordinator`
- `WorldSettingsController`

Cleanup: `GameInitializer.OnDestroy` (only for the active `Instance`) → `ServiceLocator.ClearAll()` + `RavineLog.Shutdown()`. Return to the main menu — `Bootstrap.SwitchToMainMenu`: the button is locked after the first press. The callback `SceneLoader.LoadScene(0, ReleaseServices)` is invoked after `FadeIn` and before `LoadSceneAsync`: `ReleaseServices` → `BreakUpServices` → Netcode `Shutdown` → `ClearAll` → `InitializeServices`. `OnDisable` also calls `ReleaseServices`; a repeated call has no effect.

### Main Menu — `GameSceneManagerSystem/Menu/`
- `WorldManagerUI` — list of worlds, create/delete/rename
- `WorldSettingsUI` — world config (difficulty, autosave interval, timeScale)
- `NetworkUIController` — choice of host/server/client, IP
- `SceneLaunchService` (`Core/`) — transition to the game scene

### Game Scene — `GameSceneManagerSystem/StateMachine/`
`Bootstrap` → `GameStateMachine`; states in `States/`: `BootstrapState → InitialState → LoadingState → GameState`.
Each state runs a queue of `ISetAble` services through `ServiceRegisterMachine`. Also here: `WorldStateMachine`, `PlayerStateMachine`, `AIStateMachine`.

**Service registration order (roughly):**
1. Bootstrap: `DayCycle`, `AmbientSystem`
2. Initial: `ObjectSystem`, `EntitySystem`, `MobController`
3. Loading: `MapGenerator`, `MobGenerator`, `UIInventory`, `PauseUI`

---

## ServiceLocator (`Core/ServiceLocator/`)
```
ServiceLocator.Services  — Dictionary<Type, object>  — game services
ServiceLocator.Players   — PlayerContainer            — registered players
```
`ServiceContainer.Register` returns `false` on a repeated type registration, or `null` (no replace). `ServiceLocator.Register` is the same registration, logging the failure through `RavineLogger`; all calls in the project go through it. Scene services are removed through `ServiceContainer.Unregister(instance)`, which removes only its own instance (`EntityManager.OnDestroy`, `MobGenerator.BreakUp`, `MenuMainScript.OnDestroy`).

---

## Entity System (component architecture)

### `AEntity` — base entity class (`Model/BehavioursMap/AEntity.cs`)
```
Dictionary<Type, IComponent>  — components
ReactiveProperty<bool> IsActive
ReactiveCommand<Unit> OnUpdate
```

### Key Components (`IComponent`, implementations in `Model/BehavioursMap/`)
| Component | Purpose |
|-----------|---------|
| `MainComponent` | name, prefabID, clientID |
| `TransformComponent` | entity/model transforms |
| `MovementComponent` (`BehavioursMap/Movement/`) | BaseSpeed, Acceleration, Deceleration, velocity |
| `EnergyComponent` (`BehavioursMap/Energy/`) | energy, maxEnergy (ReactiveProperty) |
| `CurrencyComponent` (`BehavioursMap/Currency/`) | currency (SafeInt + ReactiveProperty, only the server writes) |
| `AimComponent` (`BehavioursMap/Aim/`) | CrosshairDistance, PickDistance |
| `EventBusComponent` (`Model/Actions&Events/`) | EventBus for internal entity events |
| `StatePatternComponent` (`EntitySystem/`) | FSM — current state (AState) + command planner `CommandScheduler` |
| `SkillComponent` (`Model/EntitySkills/`) | Dictionary<string, ISkill> |
| `CameraComponent` (`EntitySystem/Entityes/`) | camera logic (follow + aim offset, four view sides) |

### Concrete Entities (`EntitySystem/Entityes/`)
- **`PlayerEntity`** — the player (`Player/`, initialised in `PlayerModelView.OnNetworkSpawn()`)
- **`BotEntity`** — NPC with FSM behaviours (`Bot/`)
- **`MobEntity`** — mobs; movement through `RoamMoveController` + `MobController` (Jobs) (`Mob/`)
- **`Mimic`** — an imitator entity with procedural legs (`LegPlanner`, `LegPool`, `SurfaceMotor`, `IVelocitySource`)

### Mob Streaming — `EntitySystem/MobGenerator.cs`
Chunk-based spawn/despawn of entities by player position, synchronised with `MapGenerator` chunks; diff buffers `_diffBuf`/`_pruneBuf` without allocations. The global registry of living entities is the list `global` in `EntitySystem/EntitySystem.cs`.

### Entity Commands (`EntitySystem/Command/`)
`CommandScheduler` (async queue, lives in states), `ICommand`, `MoveAlongPathCommand`, `SpawnObjectsCommand`, `PrintMessageCommand`.

### MVP Layer
- `AEntityViewModel : NetworkBehaviour` — subscribes to `Entity.IsActive`, `Entity.OnUpdate`
- `AEntityView : MonoBehaviour` — UI bindings to the ViewModel
- `PlayerModelView` — the concrete player ViewModel; spawns the camera through a ServerRpc

---

## Networking (Netcode for GameObjects)
- Architecture: **Host = Server + Client**
- `PlayerModelView.OnNetworkSpawn` — initialisation on each client
- `DayCycle` (`Core/DayCycle.cs`) — NetworkVariable<bool> isDay; the server controls time
- `CurrencyNetworkComponent` — NetworkVariable<int>, only the server writes, RPC from the client
- `PlayerController.MoveServerRpc` — movement is validated on the server
- `NetCode/NetworkSpawner.cs` — `SpawnObjectServerRpc(int prefabId, Vector3 position)`: the ID is an index into the whitelist `spawnablePrefabs` (only prefabs from `NetworkConfig.Prefabs`); the right belongs to a connected client with a `PlayerObject`, the spawner's owner, or one granted through `GrantSpawnRights(clientId)`; limit `maxSpawnsPerWindow` per `rateWindowSeconds` (by `ServerTime`) and `maxSpawnRadius` from `PlayerObject`; refusals are logged with `RavineLog.Warning`. The `Netcode` assembly references `Core` (the back reference `Core → Netcode` was removed)
- Connection: `GameSceneManagerSystem/NetworkConnectionManager.cs`, `NetworkTransportConfig.cs`, `NetworkUIController.cs`

---

## Saves (`Core/Storage/`, `Core/Settings/`)

### Layers
```
IAsyncPersistentStorage
  └── EncryptedPlayerPrefsStorage   (AES-CBC + MemoryPack, default)
  └── JsonPlayerPrefsStorage        (debug)

IFileManager<TId, TEntity>
  └── WorldStateRepository          (key: "world_data_{id}")
  └── WorldConfigRepository         (key: "world_settings_{id}")
  └── GlobalSettingsRepository      (key: "global_game_settings")
  └── ScriptFileManager             (key: "script_file_{name}")
```

### Data (`Core/Settings/WorldData/`, `WorldSettings/`, `GameSettings/`)
- **`GlobalSettings`** — quality, grass, control type, smooth camera rotation (MemoryPack)
- **`WorldState`** — seed, player position, cycleCount, inventory[], lastSaveTime
  Modified chunks are saved by `ChunkStore` (`ObjectSystem/Map/WorldGeneration/ChunkStore.cs`) to `persistentDataPath/worlds/{worldId}/chunks/{x}_{z}.chunk`: on every autosave (`AutosaveCoordinator.SubscribeBeforeSave`) and on leaving the scene, chunks with `ChunkData.NeedsSave` are written; when a chunk is generated, the file is applied on top of the procedural result (`TryRestore`), and `DetailLevel` is restored from the file. Format v1: header (magic, version, seed, chunk key, `DetailLevel`) + objects with their levels. Saved chunks are unloaded from memory together with the others. Corpses, infection payloads and food taboo marks (`ChunkFoodIndex._meat/_payloads/_taboo`) still live only in the session's memory.
- **`WorldConfiguration`** — world name, difficulty, autosave interval, timeScale

### `WorldRegistry` (`Core/Settings/WorldRegistry.cs`)
Central world manager: Create/Load/Unload/Delete/Rename/Save.
`AvailableWorlds: ObservableList<string>` — the UI subscribes to it directly.

### `AutosaveCoordinator`
Runs the autosave loop. Supports `SubscribeBeforeSave(Func<CT, UniTask>)` — hooks before saving.

---

## World Generation (`ObjectSystem/Map/WorldGeneration/`)

### `MapGenerator : MonoBehaviour, ISetAble`
- Chunk-based, infinite. ChunkSize=40, Scale=2 (units per tile)
- Perlin noise through the static `Noise` (cached octave offsets)
- `ChunkData` = heightMap[40,40] + temperatureMap[40,40] + SortedSet<Vector2Int> objectsToInst
- Updated by player position once per second; `ChunkGenerator` + `ChunkJobs/` (Burst)
- Subsystems: `GrassGenerator/`, `WaterSystem/` (incl. `RippleSystem/`), `ObjectGenerator/`

### Infinite Layers (`IEndless/`)
- `EndlessTerrain` — central Mesh of the window (3x3 chunks, MeshCollider), ring blocks of the "fir tree" along the viewing axis and two side blocks of ring 0 (dx = ±1)
- `EndlessLiquids` — LOD water blocks with self-generated mesh (rings like `EndlessTerrain`, index-based edge stitching)
- `EndlessObjects` — reuses objects through `ObjectSystem` (pool)

#### Camera View
- `ViewFacing` (`Model/BehavioursMap/View/`): `PlusZ=0, MinusZ=1, PlusX=2, MinusX=3` — the values match the billboard atlas columns 0..3. Helpers: `Forward()` / `Right()` (`int2` over (x, z)), `Yaw()`, `Rotate(±1)` along the cycle +Z → +X → −Z → −X, `Opposite()`
- `ViewOrientation` is the single source of the view (`Facing`, `PitchDegrees`, `Rotation`, event `FacingChanged`); it is written only by the local `CameraComponent` (`Set(facing, pitch)`). On a change it sets the shader globals `_ViewBasis` (forward.xz, right.xz) and `_ViewColumn`
- The chunk "fir tree" goes along the viewing axis: block centre = window centre + `dx · WindowSide · Right` + `ring · WindowSide · Forward` (`EndlessTerrain`, `EndlessLiquids`, `FarObjectRenderer`). `MapGenerator.ViewRingOf` / `ViewRingOfChunk` describe only what the camera draws
- Side blocks: the two ring-0 terrain blocks (dx = ±1) next to the window carry their objects as LOD ring 1 (`MapGenerator.SideBlockRing`), drawn by `FarObjectRenderer`
- NAL / random-tick simulation covers a square around the window out to the last LOD ring (`SimRingOf`, `WorldTickRules.nalRings`) regardless of the view; the fir tree and side blocks are only what the camera draws. Missing chunks of the square are supplied by `MapGenerator.SupplySimSquare`, from the inside out within the frame budget; `IsRetained` keeps the same square
- During a smooth turn `CameraComponent` passes the continuous yaw to `ViewOrientation.Set(facing, pitch, yaw)`: sprites and labels turn with the camera, while `Facing` (chunks, bend, billboards, input) switches at the start of the turn
- The world bend follows the viewing axis: `CurvedWorld.hlsl` computes `dot(positionWS.xz, ViewForward()) - _CurveParams.z`; `OnBeginCameraRendering` passes the origin on the axis `dot(camera.xz, Forward) + FocusDistance`
- Far-LOD billboards have four sides: `TreeBillboard.shader` takes its frame from `ViewForward()` / `ViewRight()` and the atlas column from `_ViewColumn`. After the switch to four columns the atlas must be re-baked in the `TreeMeshBaker` window; until then (with `_AtlasGrid.x < 4`) the X views reuse the Z pictures
- Agent and nest labels and the player's sprites turn through `IViewOriented` / `ViewOrientedTransform` (only when the view changes); birds through `BoidsBehaviour.ApplyViewRotations`

How to mark an object that depends on the view:
- C# component: implement `IViewOriented` (`ViewSlot`, `OnViewChanged`) and call `ViewOrientation.Register` / `Unregister` in `OnEnable` / `OnDisable`; for a plain Transform rotation use `ViewOrientedTransform.Attach(go, Yaw | Full)` from code
- Shader: include `Shaders/Include/ViewOrientation.hlsl` (`_ViewBasis`, `_ViewColumn`, `ViewForward()`, `ViewRight()`); the list of oriented shaders is `grep ViewOrientation.hlsl`
- Data: `ObjectLodSet.BillboardSideCount` (number of baked sides); `FarMode.MeshThenBillboard` marks that an object becomes flat on the far rings

### `ObjectSystem` (`ObjectSystem/ObjectSystem.cs`)
- `Dictionary<Vector2Int, ObjectInstInfo>` — global registry of placed objects
- `PoolManager` (`Core/PoolManager.cs`) — Dictionary<prefabID, Queue<ObjectInstance>>
- `ObjectInfoRegistry : ScriptableObject` — database of all objects; item SO data lives in `ScriptableData/Item/` (Environment, NPC, quests, descriptions)
- `ObjectSystem/TreeGenerator/` — trees; `ObjectInfo/NAL/` — NAL behaviour of objects
- Multi-level buildings: `ObjectInfo.Levels` (1..`ObjectInfo.MaxLevels`). The surface grid stays 2D; `ChunkData.LevelMask` (one byte per cell, bit k = occupied at level k≥1) is the flag read by `MapGenerator.IsOccupiedAt`/`ChunkData.HasLevelAt`. The registry `ChunkData.TryGetBuilding(anchor)` stores a `BuildingVolume` (a list of `LevelCell` across all levels) only for buildings with `Levels > 1`; the registry size depends on the number of buildings, not their height. Buildings are saved together with their chunk through `ChunkStore` (the levels of each object are written to the file).

### NAL (Natural Artificial Life) — `NAL_PC.cs`
A pseudo-ecosystem: objects reproduce and die with probabilities.
BehaviourType: `None | NAL | GROW`
On death or pickup — a `SpreadPattern` (spawns new objects around)

### WFC (Wave Function Collapse) — `WorldGeneration/StructGenerator/`
`WaveFunctionCollapseAlgorithm` + `StructureGenerator`: procedural generation of structures from tile rules (`TileRuleSO`, `TilePatternSO`).

---

## AI System (`EntitySystem/AI/`) — the core of the project

**Built into the main EntitySystem** (not a sandbox): entities in `AI/Entity/` have their own MVP (`EntityModel` / `EntityViewModel` / `EntityView`), components (`AI/Entity/Components/`: `BrainComponent`, `DigestionComponent`, `FitnessComponent`, `MortalityComponent`, `PerceptionComponent`, `PointsOfInterestComponent`, `SpeechComponent`, `StatsComponent`, `VisualCullingComponent`) and actions with energy/HP cost (`AI/EntityAction.cs`, catalogue `AI/ActionCatalog.cs`).

### `SharedHierarchicalBrain` (`AI/SharedAI/SharedHierarchicalBrain.cs`)
A two-level hierarchy with **weights shared by all entities** (evolution at the population level), per-entity contexts `EntityBrainContext`:
1. **Coordinator** (LSTM + DelayedPerceptron) — chooses a Goal: Survive/Hunt/Forage/Social
2. **Executor[goal]** (LSTM + DelayedPerceptron) — chooses an action from a subset (`ActionSubsets`)

Per-entity state: `SharedAI/LSTMContext.cs`, `SharedAI/PerceptronContext.cs` (state pool `RentState/ReturnState`, BPTT histories).

One brain per colony: `ColonyRegistry` → `ColonyState` (`Index`, `ColonyId`, `Brain`, `Nest`, `Pois`, `Stats`). `NestState` (channel maps in NativeArray, `NestDecayJob`) is per `ColonyState` and is not registered in the ServiceLocator; access to a colony is always explicit (`EntityManager.Colonies[i]`, `ColonyRegistry.TryGetById`, `EntityModel.Colony/ColonyId/ManagerIndex`). `MovePlanner` reads the fields of all colonies through `ColonyRegistry.Views`.

Saving: `SharedBrainSnapshot` v7, one file per colony `NeuralModelStorage.ColonyFileName(savedModelName, ColonyId)` = `{savedModelName}_c{ColonyId}` (`ModelInfo.ColonyId` is parsed from the name). An incompatible version, an unknown format, truncated data or a different architecture produce an error through `RavineLog` and a refusal to load (the colony keeps a new brain; the shared file `{savedModelName}` is used only as an explicit fallback with a warning). Any change of input/output sizes (`InputVectorizer.VectorSize`, `PlanCatalog.Count`, `CoordAux`, `ActionCatalog.Count`, LSTM/perceptron layers) requires incrementing `Version`; old snapshots become incompatible.

Time: `SimulationClock` is the only source of simulation time; Unity time is read only in `EntityManager.Update` (`SimulationClock.Advance(Time.deltaTime * simulationTimeScale)`).

### Tick Driver (decision)
- AI agents tick only from `EntityManager.Update`, synchronously, with a fixed step on `SimulationClock`: each agent is processed once every `SimulationRules.agentTickInterval` seconds of simulation time (default 1 s). `TickWindowBudget.Take` accumulates the debt `dt × N / interval` and hands out a whole number of agents per frame; when paused (dt = 0) agents do not tick, and after a freeze at most one window is caught up. `EntityModel.UpdateEntityCycle` is empty — there is no second AI tick path.
- `PlayerEntity` ticks in `EntitySystem.FixedUpdate` (`global` contains only players).
- Order within a frame: `SimulationClock.Advance` → fitness statistics (every `fitnessStatsInterval`) → `TickAgents`: snapshot + grid + flock (at the start of a window) → `BeginCycle` (perception, virology, command/plan tick, vectorisation) → instincts and utility (Burst) → colony decisions (`RunDecisions`, TD-flush of deferred decisions in `FinishDecide`) → `EndCycle` (start of the decision) → `MovePlanner.Flush` → infection spread → at the end of the window deaths/extinction/predators/`ApplyPendingGradients`/nests/castes/visibility → `MovePlanner.Flush` → `MotionSystem.Step`. The order of stages inside the tick was not changed when moving from the UniTask loop (the runner `PlayerLoopTiming.Update` ran before `ScriptRunBehaviourUpdate`).
- ProfilerMarker: `EntityManager.TickAgents/RebuildTickSnapshot/RunTickBatch/InfectionSpread/CloseTickWindow`, `EntityModel.Perception`, `EntitySpatialGrid.Rebuild/FindNearest/FindInRadius`, `ChunkFoodIndex.TryFindNearestFood`.

### Learning
- `AI/DelayedPerceptron/` — continuous-time RNN with τ-weights; Truncated BPTT (8 steps), reward-weighted; ε-greedy with adaptive epsilon driven by entropy
- `AI/SharedAI/ValueCritic.cs` — linear TD critic (reward baseline)
- `AI/SharedAI/RandomNetworkDistillation.cs` — intrinsic motivation (RND: target and predictor networks, novelty reward)
- `AI/GeneticParameters.cs` — LR, lambda, temperature and others; `AI/MimicPhenotype.cs` — mimicry phenotype
- `AI/LSTM/` — LSTMMemory; `AI/InputVectorizer.cs` — vectorisation of observations; `AI/EntityAction.cs`, `AI/ActionCatalog.cs`

### `AI/EntityManager.cs` and `AI/Population/`
- `EntityManager` (MonoBehaviour): lifecycle, tick driver, colony management (castes, leader, nests), visibility, brain saving; the public API (`SpawnEntity`, `SpawnChild`, `EvolveGenotypes`, `SeedProbeStrain`, `Entities`, `Colonies`, events) delegates to services.
- `ColonyPopulation` — spawning, deaths (`ProcessPendingDeaths`, corpses, alerting kin, terminal penalty), reproduction, swap-back SoA, colony extinction/reincarnation, predators, leader selection.
- `ColonyFood` — food growth (regeneration of ObjectInfo objects with `IsFood`), corpses. `EvolutionService` — fitness statistics, tournament partner selection, elite crossover, terminal penalty. `InfectionCoordinator` — a wrapper around `InfectionService`.
- `EntityModel` is split into partial files: `.Cycle` (tick), `.Social`, `.Directions`, `.Homeostasis`, `.Wild`, `.Behaviour`, `.Fitness`; fitness accounting — `FitnessComponent`.
- Agent commands — one file per command (`AI/Entity/Commands/*Command.cs`).
- `SimulationRules` — partial: `SimulationRules.cs` (RulesFrame, Bind, constants, validation) + `.Agent/.Colony/.Ecology/.Learning/.Movement/.Virology` (serialised fields and properties; field names were not changed, the asset stays compatible).

### AISpeech (`AI/AISpeech/`) — agent "speech"
Genetic audio profiles: `AgentAudioProfile`, `GeneticAudioProfile` (+Cache), `AgentAudioProfileBuilder`, `TimbreResolver`, `AudioParameterMapper`; synthesis: `StringToAudioGenerator` (string hash → harmonics → Jobs → AudioClip) + `AudioSynthesizer` (Burst `IJobParallelFor`, 4 wave shapes) + `StableHashService` (deterministic hash → ADSR envelope).

---

## Inventory (`Model/InventoryModel/`, `UI/Inventory/`)

- `InventoryModel` — pure model (slots, stacks, crafting)
- `EventDrivenInventoryProxy` — wrapper with events (Added/Removed/Changed)
- `UIInventory : ISetAble` — View, subscribed to PlaceEvent/PickUpEvent from the player's EventBus
- `CraftService` (`UI/CraftService.cs`) — checks recipes, fills the progress bar; `CraftPresenter` — MVP layer
- `UIDragger` — drag-and-drop (PC mouse + mobile touch)
- **LLM descriptions**: `LLMItemDescriptionService` with debounce (0.4 s), cache in `ItemDescriptionRegistry`

---

## Timers (`Core/Timer/`)

- `TimeInvoker : MonoBehaviour` (DontDestroyOnLoad, singleton) — ticks from `Update()`: `OnUpdateTimeTickedEvent`, `OnOneSyncedSecondTickedEvent` (+ unscaled variants)
- `SyncedTimer` — subscribes to TimeInvoker, supports Pause/Unpause; also `TimeChangingSource.cs`, `TimerType.cs`

---

## Terminal / Rive Scripting Language (`Terminal/`)

- `GameInitializer.cs` — entry point (see above); `Terminal.cs` — UI console with history (50 lines, ZString); `UI/Console/`
- `CommandManager` — Dictionary of commands by name/alias; `BaseCommands`, `GeneratorCommands`, `SetPlayerCommand`
- **Rive** (`Terminal/Rive/`) — a custom interpreted language:
  - Types: `int`, arrays `int[]`
  - Constructs: `if/else/end`, `for x = N to M`, `wait`, `log`, `get` (stdin), `send(interactor, val)`
  - `~command` — calls terminal commands from a script
  - `RiveParser` → AST → `RiveExecutor` (stack-based scopes, MAX 10000 ops)
  - `RiveRuntime` — program registry, built-in functions (abs/min/max/clamp/pow/sqrt/rand)
  - `InteractorRegistry` — in-game puzzles (DigitalLock, Collatz, Checksum, SequenceValidator)
- `Terminal/PDollar/` — `$P` recognizer: `GestureRecognizer` — gesture recognition → command dispatcher

---

## Input Control (`Core/NewInputSystem/`)

- `ActionMapController` — switches Action Maps: Gameplay / Pause / Inventory / UI
- `InputBindingAdapter` (`Core/`) — binds `Button.onClick` + `InputAction.performed` in one object
- `IController` — abstraction over `PCController` (mouse + keyboard) and `JoistickController`
- `DoubleTapDetector` — double-tap detector for boost
- Camera: the mouse wheel only zooms (`Camera Scroll`); the view is rotated with the mouse side buttons — Mouse4 (`<Mouse>/backButton`, `Camera Rotate Right`) clockwise (+Z → +X → −Z → −X), Mouse5 (`<Mouse>/forwardButton`, `Camera Rotate Left`) the other way; one press is one 90° step, debounce `SimulationRules.CameraFlipDebounce`. The turn is smooth by default (`GlobalSettings.smoothCameraRotation`, a toggle in the settings; duration `SimulationRules.CameraRotateSeconds`); with the toggle off it is instant. The Camera Flip action is removed

---

## Sound

- **SFX** (`SFX/`): `UISounds/`, `AmbientSounds/`, `RadioSounds/`, `MusicSoudns/`, `SFXSounds/`, `Audio/` — regular players and mixes
- **Agent speech synthesis** — see AISpeech above (`EntitySystem/AI/AISpeech/`)

## Days and Weather

- `Core/DayCycle.cs` — network day/night cycle (NetworkVariable), skybox; `Core/FogCycle.cs` — fog

---

## Boids (bird flocks) — `EntitySystem/Job/`

- `BoidsBehaviour` — orchestrator. State is in SoA `NativeArray` indexed by bird (`_pos`, `_vel`, `_acc`, `_state`, `_flock`, `_timer`, `_phase`, `_perchRef`, `_panic`, …). Flocks = prefabs; birds of a flock occupy a contiguous range `_flockStart[f].._flockStart[f+1]`. The population is fixed: there is no removal and no swap-back; a repeated `StartBoids` reuses the buffers if `numberOfEntities` and the number of prefabs have not changed
- Time — only `SimulationClock` (dt is capped by `BoidsInfo.maxDeltaTime`); flock timers (`flockRetargetInterval`) tick from the same dt
- Job chain per frame (Complete in `LateUpdate`):
  - `BirdSlotJob` (`IJob`) — frees perch slots of birds that took off, plans the flock's landing by `FlockCommand` (tree or ground)
  - `BirdGridJob` (`IJob`) — counting-sort hash grid: `CellEnd`/`Items`/`BucketOf`, `Mask = ceilpow2(2n) - 1`, bucket through `BoidHash`; coordinates outside the world are hashed into a valid bucket
  - `AccelerationJob` (`IJobParallelFor`) — prioritised steering: flocking (3×3 buckets with deduplication, neighbour limit `SimulationRules.birdMaxNeighbors`, bucket traversal from a random offset), flock target or perch point, PD height regulator above the canopy, tree avoidance, panic propagation
  - `MoveJob` (`IJobParallelForTransform`) — flight integration with wind drift, landing along a cubic Bézier, perching (swaying on a tree / hopping on the ground), take-off, turning the sprite toward the camera
- State machine (`BirdState`): `Flying → Approach → Landing → Perched → TakeOff → Flying`
- Animation is frame-based: `SimpleUnitAnimator.SetFrame(clip, index)` is called only when the frame changes (`_frameKey`)
- `BirdHeightField` — height window around the observer (terrain, canopy, perchable flags by slope and water), rebuilt when the observer moves by `recenterFraction` or on `MapGenerator.onUpdate`
- `BirdPerchIndex` — index of trees from chunk objects: `TreePerch` (Xz, YMin/YMax, Radius, 64-bit slot mask), its own counting-sort grid. `_perchRef = tree << 6 | slot` (`PerchGround`, `PerchNone` are negative). On rebuild, `PerchRemapJob` moves references to the new tree indices. Invariant: a slot bit is set ⇔ exactly one bird holds that ref; in the editor it is checked by `Debug.Assert` after every tick
- `WindField` (`ObjectSystem/Wind/`) — wind service (WindZone + MtreeWind + wind map from grass material); `WindSampler` is passed to the jobs
- Settings: `BoidsInfo : ScriptableObject` → snapshot `BirdParams` in `StartBoids`; shared limits — `SimulationRules`
- Lifecycle: `EntitySystem.SetUpBoids` (after the player appears) → `StartBoids`; `EntitySystem.BreakUp` → `DisableBoids` (jobs completed, birds hidden); `OnDestroy` frees all `NativeArray`s, `TransformAccessArray`, `BirdHeightField`, `BirdPerchIndex`. The `isTest` flag works only in the editor

### Menu Background — `GameSceneManagerSystem/Menu/BoidsMenuBackground.cs`, `Shaders/Boids/BoidsStep.compute`

- Fully on GPU: `ClearGrid → CountGrid (InterlockedAdd) → Scan` (hierarchical prefix scan in blocks of 1024, up to 4 levels) `→ AddOffsets → Scatter → Step`
- 3D grid with clamped cell coordinates, `maxNeighbors` limit, neighbour cell traversal from a random offset
- Cursor and shock wave (LitMotion), wind from `WindField`, rendering through `Graphics.RenderMeshIndirect`
- Time — `Time.unscaledDeltaTime`: in the menu scene `SimulationClock` does not advance

---

## Other Modules

- **Casino** (`EntitySystem/Casino/`) — slots: `CasinoSlots`, `CalculateWin`, `UICasinoSlots`
- **PlayerTasks** (`EntitySystem/PlayerTasks/`) — quests: `ITask`, `TaskManager`, `TaskOfDelivery`
- **StaticNPC** (`EntitySystem/StaticNPC/`) — LLM NPC chat: `IsaNPCchatBot`, `LLMGetter`
- **InteractionSystem** (`EntitySystem/InteractionSystem/`, `EntitySystem/Info/InteractionComponent/`) — interactions with objects
- **DetectorModel** (`Model/DetectorModel/`) — detector / detectable (`DetectorBehaviour`, `DetectableBehaviour`)
- **Buildings** (`Model/BuildingsBehaviours/`) — construction
- **GlobalSystems/** — `TrollMovementTransition` and global MonoBehaviours
- **View/** — `ParallaxEffect`, `PlayerDialogOutput`, `ChangeRandomSprite`
- **UI/Settings/**, **UI/Console/** — settings and console
- **Core/Utilities/** — `PriorityQueue`, `IndexedSet`, `Dence3DGrid`, `EnumerableSnapshot`, `CodeHelpers`, `Extentions`
- **Test/** — experimental code: `PointDraw` (used in `Scenes/TestScene/56u5y3454e.unity`), `PriceChart` + `Market`/`MarketCore`/`MarketSimulator` (used by `Prefabs/Market.prefab`)

---

## Key Patterns

| Pattern | Where |
|---------|-------|
| Service Locator | `ServiceLocator` (`Core/ServiceLocator/`) |
| Component | `AEntity` + `IComponent` (`Model/BehavioursMap/`) |
| State Machine | `StatePatternComponent` + `AState` + game FSM (`GameSceneManagerSystem/StateMachine/`) |
| Command | `ICommand` (`EntitySystem/Command/`) + `ICommand` (`Terminal/`) — different interfaces! |
| Repository | `WorldStateRepository`, `WorldConfigRepository`, etc. (`Core/Storage/`) |
| Observer / Reactive | R3 `ReactiveProperty`, `Subject`, `Observable` |
| Object Pool | `PoolManager` (`Core/PoolManager.cs`) |
| MVVM (partial) | `AEntityViewModel` + `AEntityView`; AI: `EntityModel`/`EntityViewModel`/`EntityView` |

---

## Known Issues (audit 2026-08, status 2026-10)

1. ~~**Two tick drivers**~~ — closed: AI ticks only from `EntityManager.Update` with a fixed `SimulationClock` step; players tick in `EntitySystem.FixedUpdate` (see "Tick Driver").
2. ~~**PoolManager does not pool**~~ — closed: free/busy are separated, `Release` checks `IsActive` and `Owner`.
3. **Spawn desync** (open): `EntitySystem.CreateMob` finds the ViewModel through `GetComponentInChildren`, `MobNAL.RunLifecycle` through `GetComponent` on the root; if they disagree, the entity is not registered.
4. ~~**Network**: authorisation in `NetworkSpawner.IsAuthorizedClient` did not work~~ — closed: owner/granted right, whitelisted int IDs, rate and radius limits (see "Networking").
5. **Leaks**: ~~R3 subscriptions of `AEntityViewModel`~~ — `DisposableBag` (`Clear` in `OnNetworkDespawn`, `Dispose` in `OnDestroy`); ~~`CommandScheduler.ResetCancellation`~~ — the old CTS is disposed; ~~`GameInitializer.OnDestroy`~~ — `ClearAll`; ~~silent `Register` failure~~ — `ServiceLocator.Register` logs the error, `Unregister` and `Replace` (returns the previous instance) were added. Open: `CommandScheduler._isProcessing` is not reset when the loop exits on cancellation.
6. **AI**: ~~`lr` × signed reward~~ — not a bug: the signed advantage is a correct PPO gradient, `lr > 0` is guaranteed (`SharedHierarchicalBrain.ResolveLearningRate`, `SimulationRules.OnValidate`); ~~fitness = AverageEntropy~~ — outdated, `EvolveGenotypes` sorts by `SelectionFitness`; ~~`new DelayedItem` every tick~~ — closed, `DecisionRing` preallocates the buffer.
7. ~~**Dead code**~~ — removed `RpcTest.cs`, `PlayerUIService.cs`, `Test/Test.cs`, root `*CommandBuffer.cs`, duplicate configs without a dot; `mono_crash.mem.6929.1.blob` was removed from the index, `*.blob` and `mono_crash*` are in `.gitignore`. `RavineNetworkManager.cs` was absent from the repository. `CraftModel.cs` was recreated by stage 2.4 (shapeless crafting) and stays; `Scripts/Test/` (`PointDraw`, `PriceChart`, `Market*`) is referenced from `TestScene` and `Market.prefab` and stays.
8. ~~**Unity time in agent logic**~~ — closed: `UniTask.Delay`/`Time.smoothDeltaTime` in `EntityManager` were replaced by `SimulationClock`.
9. ~~**R3 in the agent hot path**~~ — `StatsComponent.Health/Energy` and `_vecMaxHealth/_vecMaxEnergy` were replaced by fields + the `HealthDepleted` event / a direct call; external subscribers (`AEntity.IsActive`, `OnUpdate`, the game's `EnergyComponent`/`CurrencyComponent`) are kept.
10. ~~**God classes**~~ — `EntityManager` 1253 → 733 lines (+ `AI/Population/`), `EntityModel` 1662 → 583 + 7 partial files, `EntityCommands` → 23 files, `SimulationRules` 2149 → 1111 + 6 partial files. Physical splitting of `SimulationRules` into separate ScriptableObject assets is not done: it needs a data migration of the Unity asset.
