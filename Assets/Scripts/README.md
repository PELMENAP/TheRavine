# TheRavine

Open-world sandbox game developed in Unity.

## Философия

The Ravine — виртуальное место, где человек и ИИ живут и сосуществуют в одинаковых условиях. Игрок — такое же равноценное существо, как любое другое; мир — экосистема в духе A-Life, а не сценарий вокруг игрока.

- **Абсолютная свобода:** мир изменяем игроком и самим собой; любое существо вправе менять код и правила игры.
- **Равенство:** все существа и игроки находятся в одинаковых условиях и вынуждены адаптироваться и взаимодействовать.
- **ИИ как игрок:** в The Ravine может играть не только человек, но и искусственный интеллект.
- **Образ важнее объёма:** 3D-модели в большинстве случаев избыточны — глаз на мониторе видит 2D-проекцию и сам достраивает объём.
- **Производительность — условие свободы:** много живых существ возможно только при дешёвой симуляции.

Полный текст: [PHILOSOPHY.md](../../PHILOSOPHY.md).

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


# TheRavine — Project Architecture Summary

## Расположение
Все скрипты: `Assets/Scripts/` (483 файла `.cs`, подсчёт: `find Assets/Scripts -name '*.cs' | wc -l`). Сборки (asmdef в `Assets/Scripts`, 14): `Audio`, `BehavioursMap`, `Core`, `DetectorSystem`, `DialogGraph`, `EntitySystem`, `EventSystem`, `Info`, `Inventory`, `Netcode`, `ObjectSystem`, `SkillSystem`, `UIInventory`, `View`.

## Stack
- **Unity 6000.3** (3D, URP), **C#**, **Netcode for GameObjects** (host/server/client)
- **UniTask** — async/await everywhere instead of coroutines
- **R3** — reactive properties, observables (аналог UniRx)
- **LitMotion** — tweens/animations
- **MemoryPack** — binary serialization для сохранений
- **ZString / Cysharp.Text** — zero-alloc string building
- **ZLinq** — zero-alloc LINQ
- **Unity.Jobs + Burst** — высоконагруженные вычисления
- **LLMUnity** — локальная LLM для описаний предметов и NPC-чата
- **NaughtyAttributes** — editor utilities

---

## Точка входа и жизненный цикл

### `GameInitializer` (`Terminal/GameInitializer.cs`, DontDestroyOnLoad, Awake)
Создаёт все глобальные сервисы **до** загрузки игровой сцены:
- `RavineLogger` → `ServiceLocator.Services`
- `ActionMapController` (`Core/NewInputSystem/`)
- `GlobalSettingsController` (настройки качества, трава, управление)
- `WorldRegistry` (список миров, текущий мир)
- `AutosaveCoordinator`
- `WorldSettingsController`

Очистка: `GameInitializer.OnDestroy` (только активный `Instance`) → `ServiceLocator.ClearAll()` + `RavineLog.Shutdown()`. Выход в главное меню — `Bootstrap.SwitchToMainMenu`: кнопка блокируется на первом нажатии. Колбэк `SceneLoader.LoadScene(0, ReleaseServices)` вызывается после `FadeIn`, до `LoadSceneAsync`: `ReleaseServices` → `BreakUpServices` → Netcode `Shutdown` → `ClearAll` → `InitializeServices`. `OnDisable` вызывает `ReleaseServices` тоже; повторный вызов не действует.

### Главное меню — `GameSceneManagerSystem/Menu/`
- `WorldManagerUI` — список миров, создание/удаление/переименование
- `WorldSettingsUI` — конфиг мира (сложность, autosave interval, timeScale)
- `NetworkUIController` — выбор host/server/client, IP
- `SceneLaunchService` (`Core/`) — переход на игровую сцену

### Игровая сцена — `GameSceneManagerSystem/StateMachine/`
`Bootstrap` → `GameStateMachine`; состояния в `States/`: `BootstrapState → InitialState → LoadingState → GameState`.
Каждое состояние запускает очередь `ISetAble` сервисов через `ServiceRegisterMachine`. Там же: `WorldStateMachine`, `PlayerStateMachine`, `AIStateMachine`.

**Порядок регистрации сервисов (грубо):**
1. Bootstrap: `DayCycle`, `AmbientSystem`
2. Initial: `ObjectSystem`, `EntitySystem`, `MobController`
3. Loading: `MapGenerator`, `MobGenerator`, `UIInventory`, `PauseUI`

---

## ServiceLocator (`Core/ServiceLocator/`)
```
ServiceLocator.Services  — Dictionary<Type, object>  — игровые сервисы
ServiceLocator.Players   — PlayerContainer            — зарегистрированные игроки
```
`ServiceContainer.Register` возвращает `false` при повторной регистрации типа или `null` (без replace). `ServiceLocator.Register` — та же регистрация с ошибкой через `RavineLogger` при отказе; все вызовы в проекте идут через него. Сервисы сцены снимаются через `ServiceContainer.Unregister(instance)` — снимает только свой экземпляр (`EntityManager.OnDestroy`, `MobGenerator.BreakUp`, `MenuMainScript.OnDestroy`).

---

## Entity System (компонентная архитектура)

### `AEntity` — базовый класс сущности (`Model/BehavioursMap/AEntity.cs`)
```
Dictionary<Type, IComponent>  — компоненты
ReactiveProperty<bool> IsActive
ReactiveCommand<Unit> OnUpdate
```

### Ключевые компоненты (`IComponent`, реализации в `Model/BehavioursMap/`)
| Компонент | Назначение |
|-----------|-----------|
| `MainComponent` | имя, prefabID, clientID |
| `TransformComponent` | entity/model трансформы |
| `MovementComponent` (`BehavioursMap/Movement/`) | BaseSpeed, Acceleration, Deceleration, velocity |
| `EnergyComponent` (`BehavioursMap/Energy/`) | energy, maxEnergy (ReactiveProperty) |
| `CurrencyComponent` (`BehavioursMap/Currency/`) | валюта (SafeInt + ReactiveProperty, только сервер пишет) |
| `AimComponent` (`BehavioursMap/Aim/`) | CrosshairDistance, PickDistance |
| `EventBusComponent` (`Model/Actions&Events/`) | EventBus для внутренних событий сущности |
| `StatePatternComponent` (`EntitySystem/`) | FSM — текущее состояние (AState) + командный планировщик `CommandScheduler` |
| `SkillComponent` (`Model/EntitySkills/`) | Dictionary<string, ISkill> |
| `CameraComponent` (`EntitySystem/Entityes/`) | логика камеры (follow + aim offset) |

### Конкретные сущности (`EntitySystem/Entityes/`)
- **`PlayerEntity`** — игрок (`Player/`, init в `PlayerModelView.OnNetworkSpawn()`)
- **`BotEntity`** — NPC с FSM-поведениями (`Bot/`)
- **`MobEntity`** — мобы, движение через `RoamMoveController` + `MobController` (Jobs) (`Mob/`)
- **`Mimic`** — сущность-имитатор с процедурными ногами (`LegPlanner`, `LegPool`, `SurfaceMotor`, `IVelocitySource`)

### Стриминг мобов — `EntitySystem/MobGenerator.cs`
Chunk-based спавн/деспавн сущностей по позиции игрока, синхронизирован с чанками `MapGenerator`; дифф-буферы `_diffBuf`/`_pruneBuf` без аллокаций. Глобальный реестр живых сущностей — список `global` в `EntitySystem/EntitySystem.cs`.

### Команды сущностей (`EntitySystem/Command/`)
`CommandScheduler` (async-очередь, живёт в стейтах), `ICommand`, `MoveAlongPathCommand`, `SpawnObjectsCommand`, `PrintMessageCommand`.

### MVP-слой
- `AEntityViewModel : NetworkBehaviour` — подписывается на `Entity.IsActive`, `Entity.OnUpdate`
- `AEntityView : MonoBehaviour` — биндинги UI на ViewModel
- `PlayerModelView` — конкретная ViewModel игрока, спавнит камеру через ServerRpc

---

## Сеть (Netcode for GameObjects)
- Архитектура: **Host = Server + Client**
- `PlayerModelView.OnNetworkSpawn` — инициализация на каждом клиенте
- `DayCycle` (`Core/DayCycle.cs`) — NetworkVariable<bool> isDay, сервер управляет временем
- `CurrencyNetworkComponent` — NetworkVariable<int>, только сервер пишет, RPC от клиента
- `PlayerController.MoveServerRpc` — движение валидируется на сервере
- `NetCode/NetworkSpawner.cs` — `SpawnObjectServerRpc(int prefabId, Vector3 position)`: ID — индекс в whitelist `spawnablePrefabs` (только префабы из `NetworkConfig.Prefabs`); право — подключённый клиент с `PlayerObject`, владелец спавнера или `GrantSpawnRights(clientId)`; лимит `maxSpawnsPerWindow` за `rateWindowSeconds` (по `ServerTime`) и `maxSpawnRadius` от `PlayerObject`; отказы — `RavineLog.Warning`. Сборка `Netcode` ссылается на `Core` (обратная ссылка `Core → Netcode` удалена)
- Соединение: `GameSceneManagerSystem/NetworkConnectionManager.cs`, `NetworkTransportConfig.cs`, `NetworkUIController.cs`

---

## Сохранения (`Core/Storage/`, `Core/Settings/`)

### Слои
```
IAsyncPersistentStorage
  └── EncryptedPlayerPrefsStorage   (AES-CBC + MemoryPack, default)
  └── JsonPlayerPrefsStorage        (отладка)

IFileManager<TId, TEntity>
  └── WorldStateRepository          (ключ: "world_data_{id}")
  └── WorldConfigRepository         (ключ: "world_settings_{id}")
  └── GlobalSettingsRepository      (ключ: "global_game_settings")
  └── ScriptFileManager             (ключ: "script_file_{name}")
```

### Данные (`Core/Settings/WorldData/`, `WorldSettings/`, `GameSettings/`)
- **`GlobalSettings`** — качество, трава, тип управления (MemoryPack)
- **`WorldState`** — seed, позиция игрока, cycleCount, инвентарь[], lastSaveTime
  Состояние карты не сохраняется: трупы, payload заражения и метки табу еды (`ChunkFoodIndex._meat/_payloads/_taboo`) живут только в памяти сессии; после загрузки мира чанки генерируются из seed, трупов и их записей нет. Модифицированные чанки удерживаются в памяти `MapGenerator` (`ChunkData.IsModified`), на диск не пишутся.
- **`WorldConfiguration`** — имя мира, сложность, autosave interval, timeScale

### `WorldRegistry` (`Core/Settings/WorldRegistry.cs`)
Центральный менеджер миров: Create/Load/Unload/Delete/Rename/Save.
`AvailableWorlds: ObservableList<string>` — UI подписывается напрямую.

### `AutosaveCoordinator`
Запускает цикл автосохранения. Поддерживает `SubscribeBeforeSave(Func<CT, UniTask>)` — хуки до сохранения.

---

## Генерация мира (`ObjectSystem/Map/WorldGeneration/`)

### `MapGenerator : MonoBehaviour, ISetAble`
- Chunk-based, infinite. ChunkSize=40, Scale=2 (юниты на тайл)
- Шум Перлина через статический `Noise` (кешированные октавные офсеты)
- `ChunkData` = heightMap[40,40] + temperatureMap[40,40] + SortedSet<Vector2Int> objectsToInst
- Обновление по позиции игрока раз в секунду; `ChunkGenerator` + `ChunkJobs/` (Burst)
- Подсистемы: `GrassGenerator/`, `WaterSystem/` (вкл. `RippleSystem/`), `ObjectGenerator/`

### Бесконечные слои (`IEndless/`)
- `EndlessTerrain` — обновляет единый Mesh (3x3 чанка), MeshCollider
- `EndlessLiquids` — двигает водный plane
- `EndlessObjects` — переиспользует объекты через `ObjectSystem` (pool)

### `ObjectSystem` (`ObjectSystem/ObjectSystem.cs`)
- `Dictionary<Vector2Int, ObjectInstInfo>` — глобальный реестр размещённых объектов
- `PoolManager` (`Core/PoolManager.cs`) — Dictionary<prefabID, Queue<ObjectInstance>>
- `ObjectInfoRegistry : ScriptableObject` — база всех объектов; SO-данные предметов — `ScriptableData/Item/` (Environment, NPC, квесты, описания)
- `ObjectSystem/TreeGenerator/` — деревья; `ObjectInfo/NAL/` — NAL-поведение объектов
- Многоуровневые постройки: `ObjectInfo.Levels` (1..`ObjectInfo.MaxLevels`). Поверхностная сетка остаётся 2D; `ChunkData.LevelMask` (байт на клетку, бит k = занята на уровне k≥1) — флаг для чтения `MapGenerator.IsOccupiedAt`/`ChunkData.HasLevelAt`. Реестр `ChunkData.TryGetBuilding(anchor)` хранит `BuildingVolume` (список `LevelCell` по всем уровням) только для построек с `Levels > 1`; размер реестра зависит от числа построек, а не от высоты. Сохранение построек не реализовано (см. решение 2.2).

### Случайные тики мира — `WorldGeneration/Ticks/`
`RandomTickService` (вызов из `MapGenerator.Update` после `ApplyWorldCommands`, только при `WorldAuthority.IsAuthority`): каждые `WorldTickRules.tickSeconds` мирового времени (`SimulationClock`) Burst-джоб `RandomTickJob` выбирает в каждом чанке области (окно 3×3 + кольца `1..NalRingCount`) `cellsPerChunkPerTick` случайных клеток (в дальних кольцах в `farCellDivisor × ring` раз меньше) и отдаёт события по первичным клеткам объектов подписчикам `IRandomTickHandler`. Стоимость тика не зависит от числа объектов. Догон: чанк, вернувшийся в область, получает до `catchUpBurstRounds` доп. раундов за тик, пока не отработает `min(отсутствие, catchUpMaxSeconds) / tickSeconds` тиков (только в рантайме, не сохраняется).

### NAL (Natural Artificial Life) — `NAL_PC.cs` (`NalTickHandler`)
Псевдо-экосистема на случайных тиках: объекты размножаются/умирают по вероятностям `NalRules`.
BehaviourType: `None | NAL | GROW`; объекты с `ObjectInfo.decaySeconds > 0` без NAL/GROW гниют (вероятность `1 - exp(-meanHit / decaySeconds)` на попадание).
NAL принимает событие с вероятностью `nalDelayReference / delay`, GROW — `growAcceptChance`. Все изменения мира — через `WorldCommandBuffer`.
При смерти/подборе — `SpreadPattern` (spawn новых объектов вокруг)

### WFC (Wave Function Collapse) — `WorldGeneration/StructGenerator/`
`WaveFunctionCollapseAlgorithm` + `StructureGenerator`: процедурная генерация структур из тайловых правил (`TileRuleSO`, `TilePatternSO`).

---

## AI-система (`EntitySystem/AI/`) — ядро проекта

**Встроена в основной EntitySystem** (не sandbox): сущности `AI/Entity/` имеют собственный MVP (`EntityModel` / `EntityViewModel` / `EntityView`), компоненты (`AI/Entity/Components/`: `BrainComponent`, `DigestionComponent`, `FitnessComponent`, `MortalityComponent`, `PerceptionComponent`, `PointsOfInterestComponent`, `SpeechComponent`, `StatsComponent`, `VisualCullingComponent`) и действия с ценой энергии/HP (`AI/EntityAction.cs`, каталог `AI/ActionCatalog.cs`).

### `SharedHierarchicalBrain` (`AI/SharedAI/SharedHierarchicalBrain.cs`)
Двухуровневая иерархия, **веса общие на всех сущностей** (эволюция на уровне популяции), пер-сущностные контексты `EntityBrainContext`:
1. **Coordinator** (LSTM + DelayedPerceptron) — выбирает Goal: Survive/Hunt/Forage/Social
2. **Executor[goal]** (LSTM + DelayedPerceptron) — выбирает действие из подмножества (`ActionSubsets`)

Пер-сущностное состояние: `SharedAI/LSTMContext.cs`, `SharedAI/PerceptronContext.cs` (пул состояний `RentState/ReturnState`, BPTT-истории).

Один мозг на колонию: `ColonyRegistry` → `ColonyState` (`Index`, `ColonyId`, `Brain`, `Nest`, `Pois`, `Stats`). `NestState` (карты каналов в NativeArray, `NestDecayJob`) — per `ColonyState`, в ServiceLocator не регистрируется; доступ к колонии только явный (`EntityManager.Colonies[i]`, `ColonyRegistry.TryGetById`, `EntityModel.Colony/ColonyId/ManagerIndex`). `MovePlanner` читает поля всех колоний через `ColonyRegistry.Views`.

Сохранение: `SharedBrainSnapshot` v7, файл на колонию `NeuralModelStorage.ColonyFileName(savedModelName, ColonyId)` = `{savedModelName}_c{ColonyId}` (`ModelInfo.ColonyId` разбирается из имени). Несовместимая версия, неизвестный формат, обрезанные данные или другая архитектура — ошибка через `RavineLog` и отказ от загрузки (колония остаётся с новым мозгом; общий файл `{savedModelName}` используется только как явный fallback с предупреждением). Любое изменение размеров входа/выхода (`InputVectorizer.VectorSize`, `PlanCatalog.Count`, `CoordAux`, `ActionCatalog.Count`, слои LSTM/перцептронов) роняет `BrainArchitectureVersionTests` — нужен инкремент `Version`, старые снапшоты несовместимы.

Время: `SimulationClock` — единственный источник времени симуляции; Unity-время читается только в `EntityManager.Update` (`SimulationClock.Advance(Time.deltaTime * simulationTimeScale)`).

### Драйвер тика (решение)
- AI-агенты тикают только из `EntityManager.Update`, синхронно, с фиксированным шагом по `SimulationClock`: каждый агент обрабатывается раз в `SimulationRules.agentTickInterval` секунд симуляции (по умолчанию 1 с). `TickWindowBudget.Take` накапливает долг `dt × N / interval` и отдаёт целое число агентов на кадр; при паузе (dt = 0) агенты не тикают, после фриза догоняется не больше одного окна. `EntityModel.UpdateEntityCycle` пустой — второго пути тика AI нет.
- `PlayerEntity` тикает в `EntitySystem.FixedUpdate` (`global` содержит только игроков).
- Порядок в кадре: `SimulationClock.Advance` → статистика fitness (раз в `fitnessStatsInterval`) → `TickAgents`: снапшот + сетка + стая (в начале окна) → `BeginCycle` (перцепция, вирусология, тик команды/плана, векторизация) → инстинкты и utility (Burst) → решения колоний (`RunDecisions`, TD-flush отложенных решений в `FinishDecide`) → `EndCycle` (старт решения) → `MovePlanner.Flush` → распространение инфекции → в конце окна смерти/вымирания/хищники/`ApplyPendingGradients`/гнёзда/касты/видимость → `MovePlanner.Flush` → `MotionSystem.Step`. Порядок стадий внутри тика не менялся при переносе с UniTask-цикла (раннер `PlayerLoopTiming.Update` выполнялся до `ScriptRunBehaviourUpdate`).
- ProfilerMarker: `EntityManager.TickAgents/RebuildTickSnapshot/RunTickBatch/InfectionSpread/CloseTickWindow`, `EntityModel.Perception`, `EntitySpatialGrid.Rebuild/FindNearest/FindInRadius`, `ChunkFoodIndex.TryFindNearestFood`.

### Обучение
- `AI/DelayedPerceptron/` — continuous-time RNN с τ-весами; Truncated BPTT (8 шагов), reward-weighted; ε-greedy с adaptive epsilon по entropy
- `AI/SharedAI/ValueCritic.cs` — линейный TD-критик (baseline для reward)
- `AI/SharedAI/RandomNetworkDistillation.cs` — intrinsic motivation (RND: target+predictor сети, novelty reward)
- `AI/GeneticParameters.cs` — LR, lambda, temperature и пр.; `AI/MimicPhenotype.cs` — фенотип мимикрии
- `AI/LSTM/` — LSTMMemory; `AI/InputVectorizer.cs` — векторизация наблюдений; `AI/EntityAction.cs`, `AI/ActionCatalog.cs`

### `AI/EntityManager.cs` и `AI/Population/`
- `EntityManager` (MonoBehaviour): жизненный цикл, драйвер тика, управление колониями (касты, лидер, гнёзда), видимость, сохранение мозгов; публичный API (`SpawnEntity`, `SpawnChild`, `EvolveGenotypes`, `SeedProbeStrain`, `Entities`, `Colonies`, события) делегирует сервисам.
- `ColonyPopulation` — спавн, смерти (`ProcessPendingDeaths`, трупы, оповещение сородичей, терминальный штраф), размножение, swap-back SoA, вымирание/реинкарнация колоний, хищники, выбор лидера.
- `ColonyFood` — рост еды (регенерация объектов ObjectInfo с `IsFood`), трупы. `EvolutionService` — fitness-статистика, турнирный выбор партнёра, элитный кроссовер, терминальный штраф. `InfectionCoordinator` — обёртка `InfectionService`.
- `EntityModel` разнесён по partial-файлам: `.Cycle` (тик), `.Social`, `.Directions`, `.Homeostasis`, `.Wild`, `.Behaviour`, `.Fitness`; учёт fitness — `FitnessComponent`.
- Команды агентов — по файлу на команду (`AI/Entity/Commands/*Command.cs`).
- `SimulationRules` — partial: `SimulationRules.cs` (RulesFrame, Bind, константы, валидация) + `.Agent/.Colony/.Ecology/.Learning/.Movement/.Virology` (сериализуемые поля и свойства; имена полей не менялись, ассет совместим).

### AISpeech (`AI/AISpeech/`) — «речь» агентов
Генетические аудио-профили: `AgentAudioProfile`, `GeneticAudioProfile` (+Cache), `AgentAudioProfileBuilder`, `TimbreResolver`, `AudioParameterMapper`; синтез: `StringToAudioGenerator` (hash строки → гармоники → Jobs → AudioClip) + `AudioSynthesizer` (Burst `IJobParallelFor`, 4 формы волны) + `StableHashService` (детерминированный хеш → ADSR огибающая).

---

## Инвентарь (`Model/InventoryModel/`, `UI/Inventory/`)

- `InventoryModel` — чистая модель (слоты, стаки, крафт)
- `EventDrivenInventoryProxy` — обёртка с событиями (Added/Removed/Changed)
- `UIInventory : ISetAble` — View, подписан на PlaceEvent/PickUpEvent из EventBus игрока
- `CraftService` (`UI/CraftService.cs`) — проверяет рецепты, заполняет прогресс-бар; `CraftPresenter` — MVP-слой
- `UIDragger` — drag-and-drop (PC мышь + mobile touch)
- **LLM-описания**: `LLMItemDescriptionService` с debounce (0.4s), кеш в `ItemDescriptionRegistry`

---

## Таймеры (`Core/Timer/`)

- `TimeInvoker : MonoBehaviour` (DontDestroyOnLoad, singleton) — тикает из `Update()`: `OnUpdateTimeTickedEvent`, `OnOneSyncedSecondTickedEvent` (+ unscaled варианты)
- `SyncedTimer` — подписывается на TimeInvoker, поддерживает Pause/Unpause; также `TimeChangingSource.cs`, `TimerType.cs`

---

## Терминал / Скриптовый язык Rive (`Terminal/`)

- `GameInitializer.cs` — точка входа (см. выше); `Terminal.cs` — UI консоль с историей (50 строк, ZString); `UI/Console/`
- `CommandManager` — Dictionary команд по имени/псевдониму; `BaseCommands`, `GeneratorCommands`, `SetPlayerCommand`
- **Rive** (`Terminal/Rive/`) — собственный интерпретируемый язык:
  - Типы: `int`, массивы `int[]`
  - Конструкции: `if/else/end`, `for x = N to M`, `wait`, `log`, `get` (stdin), `send(interactor, val)`
  - `~команда` — вызов терминальных команд из скрипта
  - `RiveParser` → AST → `RiveExecutor` (stack-based scopes, MAX 10000 ops)
  - `RiveRuntime` — реестр программ, built-in функции (abs/min/max/clamp/pow/sqrt/rand)
  - `InteractorRegistry` — игровые головоломки (DigitalLock, Collatz, Checksum, SequenceValidator)
- `Terminal/PDollar/` — `$P` recognizer: `GestureRecognizer` — распознавание жестов → диспетчер команд

---

## Управление вводом (`Core/NewInputSystem/`)

- `ActionMapController` — переключение Action Maps: Gameplay / Pause / Inventory / UI
- `InputBindingAdapter` (`Core/`) — связывает `Button.onClick` + `InputAction.performed` в одном объекте
- `IController` — абстракция над `PCController` (мышь+клавиатура) и `JoistickController`
- `DoubleTapDetector` — детектор двойного нажатия для буста

---

## Звук

- **SFX** (`SFX/`): `UISounds/`, `AmbientSounds/`, `RadioSounds/`, `MusicSoudns/`, `SFXSounds/`, `Audio/` — обычные плееры/миксы
- **Синтез речи агентов** — см. AISpeech выше (`EntitySystem/AI/AISpeech/`)

## Дни/погода

- `Core/DayCycle.cs` — сетевой цикл дня/ночи (NetworkVariable), skybox; `Core/FogCycle.cs` — туман

---

## Boids (стаи птиц) — `EntitySystem/Job/`

- `BoidsBehaviour` — оркестратор. Состояние в SoA `NativeArray` по индексу птицы (`_pos`, `_vel`, `_acc`, `_state`, `_flock`, `_timer`, `_phase`, `_perchRef`, `_panic`, …). Стаи = префабы, птицы стаи лежат непрерывным диапазоном `_flockStart[f].._flockStart[f+1]`. Популяция фиксирована: удаления и swap-back нет, повторный `StartBoids` переиспользует буферы, если не изменились `numberOfEntities` и число префабов
- Время — только `SimulationClock` (dt ограничен `BoidsInfo.maxDeltaTime`); таймеры стай (`flockRetargetInterval`) тикают от того же dt
- Цепочка джобов за кадр (Complete в `LateUpdate`):
  - `BirdSlotJob` (`IJob`) — освобождение слотов присады у взлетевших, планирование посадки стаи по `FlockCommand` (дерево или земля)
  - `BirdGridJob` (`IJob`) — counting-sort hash grid: `CellEnd`/`Items`/`BucketOf`, `Mask = ceilpow2(2n) - 1`, бакет через `BoidHash`; координаты вне мира хешируются в валидный бакет
  - `AccelerationJob` (`IJobParallelFor`) — prioritized steering: flocking (3×3 бакета с дедупликацией, лимит соседей `SimulationRules.birdMaxNeighbors`, обход бакета со случайного смещения), цель стаи или точка присады, PD-регулятор высоты над кронами, обход деревьев, распространение паники
  - `MoveJob` (`IJobParallelForTransform`) — интеграция полёта со сносом ветром, посадка по кубической Безье, присада (покачивание на дереве / подскоки на земле), взлёт, разворот спрайта к камере
- State machine (`BirdState`): `Flying → Approach → Landing → Perched → TakeOff → Flying`
- Анимация — покадровая: `SimpleUnitAnimator.SetFrame(clip, index)` вызывается только при смене кадра (`_frameKey`)
- `BirdHeightField` — окно высот вокруг наблюдателя (terrain, canopy, флаги perchable по уклону и воде), пересборка при смещении наблюдателя на `recenterFraction` или по `MapGenerator.onUpdate`
- `BirdPerchIndex` — индекс деревьев из объектов чанков: `TreePerch` (Xz, YMin/YMax, Radius, 64-битная маска слотов), собственный counting-sort grid. `_perchRef = tree << 6 | slot` (`PerchGround`, `PerchNone` — отрицательные). При пересборке `PerchRemapJob` переносит ссылки на новые индексы деревьев. Инвариант: бит слота занят ⇔ ровно одна птица держит этот ref; в редакторе проверяется `Debug.Assert` после каждого тика
- `WindField` (`ObjectSystem/Wind/`) — сервис ветра (WindZone + MtreeWind + wind map из материала травы), в джобы передаётся `WindSampler`
- Настройки: `BoidsInfo : ScriptableObject` → снимок `BirdParams` в `StartBoids`; общие лимиты — `SimulationRules`
- Жизненный цикл: `EntitySystem.SetUpBoids` (после появления игрока) → `StartBoids`; `EntitySystem.BreakUp` → `DisableBoids` (джобы завершены, птицы скрыты); `OnDestroy` освобождает все `NativeArray`, `TransformAccessArray`, `BirdHeightField`, `BirdPerchIndex`. Флаг `isTest` работает только в редакторе

### Фон меню — `GameSceneManagerSystem/Menu/BoidsMenuBackground.cs`, `Shaders/Boids/BoidsStep.compute`

- Полностью на GPU: `ClearGrid → CountGrid (InterlockedAdd) → Scan` (иерархический префикс-скан блоками по 1024, до 4 уровней) `→ AddOffsets → Scatter → Step`
- 3D-сетка с clamp координат ячейки, лимит `maxNeighbors`, обход соседних ячеек со случайного смещения
- Курсор и ударная волна (LitMotion), ветер из `WindField`, рендер через `Graphics.RenderMeshIndirect`
- Время — `Time.unscaledDeltaTime`: в сцене меню `SimulationClock` не продвигается

---

## Прочие модули

- **Casino** (`EntitySystem/Casino/`) — слоты: `CasinoSlots`, `CalculateWin`, `UICasinoSlots`
- **PlayerTasks** (`EntitySystem/PlayerTasks/`) — задания: `ITask`, `TaskManager`, `TaskOfDelivery`
- **StaticNPC** (`EntitySystem/StaticNPC/`) — LLM-чат NPC: `IsaNPCchatBot`, `LLMGetter`
- **InteractionSystem** (`EntitySystem/InteractionSystem/`, `EntitySystem/Info/InteractionComponent/`) — взаимодействия с объектами
- **DetectorModel** (`Model/DetectorModel/`) — детектор/детектируемые (`DetectorBehaviour`, `DetectableBehaviour`)
- **Buildings** (`Model/BuildingsBehaviours/`) — строительство
- **GlobalSystems/** — `TrollMovementTransition` и глобальные монобехи
- **View/** — `ParallaxEffect`, `PlayerDialogOutput`, `ChangeRandomSprite`
- **UI/Settings/**, **UI/Console/** — настройки и консоль
- **Core/Utilities/** — `PriorityQueue`, `IndexedSet`, `Dence3DGrid`, `EnumerableSnapshot`, `CodeHelpers`, `Extentions`
- **Test/** — экспериментальный код: `PointDraw` (используется в `Scenes/TestScene/56u5y3454e.unity`), `PriceChart` + `Market`/`MarketCore`/`MarketSimulator` (используются `Prefabs/Market.prefab`)
- **Assets/Tests/EditMode/** — EditMode-тесты (`EntitySystem.Tests.EditMode.asmdef`, Editor-only): `TickWindowBudgetTests`, `TerminalPenaltyTests`, `BrainArchitectureVersionTests`, `ServiceContainerTests`, `DecisionRingTests`, `RunningMeanStdTests`, `BrainLearningTests` (знак градиента, изоляция LSTM, zero-alloc батча), `StatsMortalityTests`, `HotPathAllocationTests`, `RulesDefaultsTests`, `ColonyRegistryTests`, `SnapshotVersionTests`, `FitnessComponentTests`, `SoaSwapBackTests`, `CorpseBiteTests` (порции укуса трупа, инфекция один раз), `ViralPayloadPoolTests` (`Release` идемпотентен); `AllocationAssert` — общий хелпер; zero-alloc через `GC.GetAllocatedBytesForCurrentThread` + `Is.Not.AllocatingGCMemory`

---

## Ключевые паттерны

| Паттерн | Где |
|---------|-----|
| Service Locator | `ServiceLocator` (`Core/ServiceLocator/`) |
| Component | `AEntity` + `IComponent` (`Model/BehavioursMap/`) |
| State Machine | `StatePatternComponent` + `AState` + FSM игры (`GameSceneManagerSystem/StateMachine/`) |
| Command | `ICommand` (`EntitySystem/Command/`) + `ICommand` (`Terminal/`) — разные интерфейсы! |
| Repository | `WorldStateRepository`, `WorldConfigRepository`, etc. (`Core/Storage/`) |
| Observer / Reactive | R3 `ReactiveProperty`, `Subject`, `Observable` |
| Object Pool | `PoolManager` (`Core/PoolManager.cs`) |
| MVVM (частично) | `AEntityViewModel` + `AEntityView`; AI: `EntityModel`/`EntityViewModel`/`EntityView` |

---

## Известные проблемы (аудит 2026-08, статус 2026-10)

1. ~~**Два драйвера тика**~~ — закрыто: AI — только `EntityManager.Update` с фиксированным шагом `SimulationClock`, игроки — `EntitySystem.FixedUpdate` (см. «Драйвер тика»).
2. ~~**PoolManager не пулит**~~ — закрыто: free/busy разделены, `Release` проверяет `IsActive` и `Owner`.
3. **Спавн-рассинхрон** (открыто): `EntitySystem.CreateMob` ищет ViewModel через `GetComponentInChildren`, `MobNAL.RunLifecycle` — через `GetComponent` на корне; при расхождении сущность не регистрируется.
4. ~~**Сеть**: авторизация `NetworkSpawner.IsAuthorizedClient` не работает~~ — закрыто: владелец/выданное право, whitelist int-ID, лимит частоты и радиуса (см. «Сеть»).
5. **Утечки**: ~~R3-подписки `AEntityViewModel`~~ — `DisposableBag` (`Clear` в `OnNetworkDespawn`, `Dispose` в `OnDestroy`); ~~`CommandScheduler.ResetCancellation`~~ — старый CTS диспоузится; ~~`GameInitializer.OnDestroy`~~ — `ClearAll`; ~~молчаливый отказ `Register`~~ — `ServiceLocator.Register` логирует ошибку, добавлены `Unregister` и `Replace` (возвращает прежний экземпляр). Открыто: `CommandScheduler._isProcessing` не сбрасывается при выходе цикла по отмене.
6. **AI**: ~~`lr` × знаковый reward~~ — не баг: advantage со знаком — корректный PPO-градиент, `lr > 0` гарантирован (`SharedHierarchicalBrain.ResolveLearningRate`, `SimulationRules.OnValidate`), знак терминального штрафа зафиксирован `TerminalPenaltyTests`; ~~fitness = AverageEntropy~~ — устарело, `EvolveGenotypes` сортирует по `SelectionFitness`; ~~`new DelayedItem` каждый тик~~ — закрыто, `DecisionRing` предаллоцирует буфер.
7. ~~**Мёртвый код**~~ — удалены `RpcTest.cs`, `PlayerUIService.cs`, `Test/Test.cs`, корневые `*CommandBuffer.cs`, дубли конфигов без точки; `mono_crash.mem.6929.1.blob` снят с индекса, `*.blob` и `mono_crash*` в `.gitignore`. `RavineNetworkManager.cs` в репозитории отсутствовал. `CraftModel.cs` пересоздан этапом 2.4 (безформенный крафт, `CraftModelTests`) и остаётся; `Scripts/Test/` (`PointDraw`, `PriceChart`, `Market*`) ссылается из `TestScene` и `Market.prefab` и остаётся.
8. ~~**Unity-время в логике агентов**~~ — закрыто: `UniTask.Delay`/`Time.smoothDeltaTime` в `EntityManager` заменены на `SimulationClock`.
9. ~~**R3 в горячем пути агента**~~ — `StatsComponent.Health/Energy` и `_vecMaxHealth/_vecMaxEnergy` заменены полями + событием `HealthDepleted`/прямым вызовом; внешние подписчики (`AEntity.IsActive`, `OnUpdate`, игровые `EnergyComponent`/`CurrencyComponent`) сохранены.
10. ~~**God-классы**~~ — `EntityManager` 1253 → 733 строки (+ `AI/Population/`), `EntityModel` 1662 → 583 + 7 partial, `EntityCommands` → 23 файла, `SimulationRules` 2149 → 1111 + 6 partial. Физическое разделение `SimulationRules` на отдельные ScriptableObject-ассеты не сделано: нужна миграция данных ассета в Unity.
