# Core: lifecycle, services, saves, input, sound

Root: `Assets/Scripts/Core/`, `Assets/Scripts/GameSceneManagerSystem/`, `Assets/Scripts/SFX/`.
Assemblies: `Core`, `Audio`; `Assets/Scripts/GameSceneManagerSystem/` is in `Assembly-CSharp`.
Tests: `Assets/Tests/EditMode/ServiceContainerTests.cs`, `Assets/Tests/EditMode/AtomicFileTests.cs`,
`Assets/Tests/EditMode/RavineLoggerTests.cs`, `Assets/Tests/EditMode/WorldRegistryPathTests.cs`, `Assets/Tests/EditMode/WorldAuthorityTests.cs`.

## Entry points

| Type | File | Role |
|---|---|---|
| `GameInitializer` | `Assets/Scripts/Terminal/GameInitializer.cs` | DontDestroyOnLoad; creates the global services in `Awake`, before the game scene loads |
| `Bootstrap` | `Assets/Scripts/GameSceneManagerSystem/StateMachine/Bootstrap.cs` | Game scene start; return to the main menu (`SwitchToMainMenu`) |
| `GameStateMachine` | `Assets/Scripts/GameSceneManagerSystem/StateMachine/GameStateMachine.cs` | Runs the scene states |
| `ServiceRegisterMachine` | `Assets/Scripts/GameSceneManagerSystem/StateMachine/ServiceRegisterMachine.cs` | Runs the queue of `ISetAble` services of a state |
| `ServiceLocator` | `Assets/Scripts/Core/ServiceLocator/ServiceLocator.cs` | `Services` (by type) and `Players` (`PlayerContainer`) |
| `SimulationClock` | `Assets/Scripts/Core/SimulationClock.cs` | The only source of simulation time |
| `WorldAuthority` | `Assets/Scripts/Core/WorldAuthority.cs` | `IsAuthority`, `HasRemotePeers` |
| `SkyCover` | `Assets/Scripts/Core/SkyCover.cs` | Static cloud cover and rain over the camera, written by `WeatherView`; visuals only |
| `RavineLogger` | `Assets/Scripts/Core/RavineLogger.cs` | Buffered file log; static facade in `Assets/Scripts/Core/RavineLog.cs` |
| `WorldRegistry` | `Assets/Scripts/Core/Settings/WorldRegistry.cs` | Create / load / unload / delete / rename / save worlds |
| `AutosaveCoordinator` | `Assets/Scripts/Core/Settings/WorldData/AutosaveCoordinator.cs` | Autosave loop, `SubscribeBeforeSave` hooks |
| `ActionMapController` | `Assets/Scripts/Core/NewInputSystem/ActionMapController.cs` | Switches action maps: Gameplay / Pause / Inventory / UI |
| `PoolManager` | `Assets/Scripts/Core/PoolManager.cs` | Object pool by prefab id |
| `AudioService` | `Assets/Scripts/SFX/Audio/AudioService.cs` | Sound playback by channel |

## Lifecycle

1. `GameInitializer.Awake` registers: `RavineLogger`, `ActionMapController`, `GlobalSettingsController`, `WorldRegistry`, `AutosaveCoordinator`, `WorldSettingsController`.
2. Main menu: `Assets/Scripts/GameSceneManagerSystem/MainSceneManagerSystem/MenuMainScript.cs`; world list UI in `Assets/Scripts/UI/Settings/`.
3. `SceneLaunchService` (`Assets/Scripts/Core/SceneLaunchService.cs`) switches to the game scene; `SceneLoader` is in `Assets/Scripts/Core/SceneTransition.cs`.
4. Game scene: `Bootstrap` → `GameStateMachine`; states in `Assets/Scripts/GameSceneManagerSystem/States/`.
5. State order: `BootstrapState` → `InitialState` → `LoadingState` → `GameState`.
6. Service order, roughly: Bootstrap `DayCycle`, `AmbientSystem`; Initial `ObjectSystem`, `EntitySystem`, `MobController`; Loading `MapGenerator`, `MobGenerator`, `UIInventory`, `PauseUI`.
7. Bootstrap.SwitchToMainMenu saves the world before starting the fade. A failed checkpoint leaves the scene running.
8. ReleaseServices runs after the fade and before LoadSceneAsync.
9. `ReleaseServices`: `EntityManager` disabled → `BreakUpServices` → `EntityManager.ReleaseServices` → Netcode `Shutdown` → `ClearAll`.
   It then runs `InitializeServices` for an explicit return to the menu.
10. Each step of `ReleaseServices` is guarded: a failing step is logged and the next ones still run.
11. The game scene keeps running for several frames after `ReleaseServices`, until the menu scene activates.
12. `GameInitializer.OnDestroy` (active `Instance` only): `ServiceLocator.ClearAll()` and `RavineLog.Shutdown()`.

## Services

- New services register in `ServiceLocator`.
- Legacy static `Instance` classes, not a pattern for new code: `MusicSystem`, `AmbientSystem`, `AudioService`, `SFXSystem`, `UISoundSystem`,
  `RippleStampSystem`, `GameInitializer`, `FaderOnTransit`, `DialogSystem`.
- `ServiceContainer.Register` returns `false` on a repeated type or on `null`; it never replaces.
- All registrations go through `ServiceLocator.Register`, which logs a failure through `RavineLogger`.
- Scene services leave through `ServiceContainer.Unregister(instance)`, which removes only that instance.
- `Replace` returns the previous instance.
- `ServiceContainer.Clear` disposes `IDisposable` services, except Unity objects; one failing `Dispose` does not stop the rest.
- A scene service that owns native memory is unregistered by its owner before `ClearAll` and disposed in the owner's `OnDestroy`.

## Saves

- Storage layer: `IAsyncPersistentStorage` in `Assets/Scripts/Core/Storage/IAsyncPersistentStorage.cs`.
- Default implementation: `EncryptedPlayerPrefsStorage` (AES-CBC + MemoryPack); `EncryptionUtility` is obfuscation, not security.
- Repository layer: `IFileManager` in `Assets/Scripts/Core/Storage/IFileManager.cs`.
- `WorldRegistry` serializes checkpoint requests from autosave, explicit save, pause and orderly scene exit. Before-save hooks bind to the loaded world session.
- A population provider captures on the main thread before chunk flush. Population publication follows successful player, codex, configuration and chunk saves.
- `PopulationRepository` stores version 3 in `worlds/{worldId}/population.bin` and reads versions 1 and 2 (records without stress, or without the help log).
  Seed and world creation time bind the file across renames.
- A missing file is legacy state. Invalid bounds, references or versions cause a load error and preserve the file. A valid empty file is a saved population.
- `WorldStateRepository` key `world_data_{id}`; `WorldConfigRepository` key `world_settings_{id}`.
- `GlobalSettingsRepository` key `global_game_settings`; `ScriptFileManager` key `script_file_{name}`.
- `GlobalSettings`: quality, grass, control type, smooth camera rotation.
- `WorldState`: seed, player position, cycle count, inventory, last save time, rule multipliers, player skills, `CodexSave` (laws, opposition, price estimator; see `docs/map/codex.md`).
- `WorldConfiguration`: world name, difficulty, autosave interval, time scale.
- File writes go through `AtomicFile` (`Assets/Scripts/Core/Storage/AtomicFile.cs`): temp file, then move.
- Chunk files are written by `ChunkStore`; see `docs/map/world.md`.
- `WorldRegistry.AvailableWorlds` is observed directly by the UI.
- World names are validated by `WorldRegistry.IsValidWorldName` and `TryGetSafeWorldDirectory` before any delete or move.

## Time

- `SimulationClock.Advance` is called only from `EntityManager.Update`.
- `TimeInvoker` (`Assets/Scripts/Core/Timer/TimeInvoker.cs`): DontDestroyOnLoad singleton, per-frame and per-second events, scaled and unscaled.
- `SyncedTimer` (`Assets/Scripts/Core/Timer/SyncedTimer.cs`): subscribes to `TimeInvoker`, supports pause.
- `DayCycle` (`Assets/Scripts/Core/DayCycle.cs`): network day / night, server-written; `FogCycle` (`Assets/Scripts/Core/FogCycle.cs`): fog.
- `DayCycle.ApplyVisuals` multiplies sun intensity by `1 - SkyCover.Value * cloudDim`; `FogCycle.Apply` multiplies density by `1 + SkyCover.Rain * fogBoost`.
- `DayCycle.NormalizedTime` is read by `WeatherEvent` for the heat of the day (weather: `docs/map/map-events.md`).

## Input

- Generated wrapper: `Assets/Scripts/Core/NewInputSystem/GameInput.cs`. Do not read or edit it.
- Source of the wrapper: `Assets/Scripts/Core/NewInputSystem/InputAction.inputactions`.
- `InputBindingAdapter` (`Assets/Scripts/Core/InputBindingAdapter.cs`) binds a UI button and an input action in one object.
- Player controllers and camera input: `docs/map/player-net.md`.

## Sound

- Folders under `Assets/Scripts/SFX/`: `Audio`, `UISounds`, `AmbientSounds`, `RadioSounds`, `MusicSoudns`, `SFXSounds`.
- `AmbientSystem`: `Assets/Scripts/SFX/AmbientSounds/AmbientSystem.cs`.
- Agent speech synthesis is not here: `docs/map/ai.md`, section Speech.

## Utilities

- `Assets/Scripts/Core/Utilities/`: `PriorityQueue`, `IndexedSet`, `IntDictionary`, `LongDictionary`, `Position2Int`, `EnumerableSnapshot`.
- `Position2Int.Pack` is the cell and chunk key used across the project.
- `RavineRandom` is declared in `Assets/Scripts/Model/BehavioursMap/Random/FastRandom.cs`.

## Tooling

- CI: `.github/workflows/unity-tests.yml` compiles the project and runs the EditMode tests on push to `claude/**`; it is skipped without the UNITY_LICENSE secret.

## Invariants

- Simulation code reads time from `SimulationClock`, never from Unity time.
- Simulation code never reads `SkyCover`; it is written and read by visuals only.
- Nothing registers a service by writing into the container directly.
- `OnDisable` of `Bootstrap` releases services without creating replacements. A repeated release has no effect.
- `BreakUp` never frees memory that an `Update` of a still-loaded scene object can reach; it stops work and unsubscribes.
- `BreakUp` always invokes its callback, also after a failure; otherwise the services after it are not broken up.
- `SceneLoader.LoadScene` loads the scene even if the fade does not report completion or the `beforeLoad` callback throws.

## Hot paths

None in this area. `RavineLogger` is on the server path of refused requests: no string formatting per refused request.

## Open issues

- `CommandScheduler` does not reset `_isProcessing` when its loop exits on cancellation.
