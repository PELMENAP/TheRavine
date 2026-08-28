# TheRavine

Open-world sandbox game developed in Unity.

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
Все скрипты: `Assets/Scripts/` (~364 файла). Сборки (asmdef): `Core`, `BehavioursMap`, `EntitySystem`, `ObjectSystem`, `UIInventory`.

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

⚠️ Очистка сервисов в `OnDestroy` закомментирована — при перезаходе возможны «зомби»-сервисы (см. «Известные проблемы»).

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
`ServiceContainer.Register` молча игнорирует повторную регистрацию (нет replace) — важно при перезаходе на сцену.

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
- `NetCode/NetworkSpawner.cs` — ServerRpc-спавн по имени префаба (⚠️ авторизация не работает — см. «Известные проблемы»); `NetCode/RavineNetworkManager.cs` — мёртвый код (полностью закомментирован, старый UNet)
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

### NAL (Natural Artificial Life) — `NAL_PC.cs`
Псевдо-экосистема: объекты размножаются/умирают по вероятностям.
BehaviourType: `None | NAL | GROW`
При смерти/подборе — `SpreadPattern` (spawn новых объектов вокруг)

### WFC (Wave Function Collapse) — `WorldGeneration/StructGenerator/`
`WaveFunctionCollapseAlgorithm` + `StructureGenerator`: процедурная генерация структур из тайловых правил (`TileRuleSO`, `TilePatternSO`).

---

## AI-система (`EntitySystem/AI/`) — ядро проекта

**Встроена в основной EntitySystem** (не sandbox): сущности `AI/Entity/` имеют собственный MVP (`EntityModel` / `EntityViewModel` / `EntityView`), компоненты (`AI/Entity/Components/`: `BrainComponent`, `PerceptionComponent`, `MortalityComponent`, `StatsComponent`, `SpeechComponent`, `PointsOfInterestComponent`, `VisualCullingComponent`) и состояния действий (`AI/Entity/State/EntityActionState.cs` — действия с ценой энергии/HP).

### `SharedHierarchicalBrain` (`AI/SharedAI/SharedHierarchicalBrain.cs`)
Двухуровневая иерархия, **веса общие на всех сущностей** (эволюция на уровне популяции), пер-сущностные контексты `EntityBrainContext`:
1. **Coordinator** (LSTM + DelayedPerceptron) — выбирает Goal: Survive/Hunt/Forage/Social
2. **Executor[goal]** (LSTM + DelayedPerceptron) — выбирает действие из подмножества (`ActionSubsets`)

Пер-сущностное состояние: `SharedAI/LSTMContext.cs`, `SharedAI/PerceptronContext.cs` (пул состояний `RentState/ReturnState`, BPTT-истории).

### Обучение
- `AI/DelayedPerceptron/` — continuous-time RNN с τ-весами; Truncated BPTT (8 шагов), reward-weighted; ε-greedy с adaptive epsilon по entropy
- `AI/SharedAI/ValueCritic.cs` — линейный TD-критик (baseline для reward)
- `AI/SharedAI/RandomNetworkDistillation.cs` — intrinsic motivation (RND: target+predictor сети, novelty reward)
- `AI/GeneticParameters.cs` — LR, lambda, temperature и пр.; `AI/MimicPhenotype.cs` — фенотип мимикрии
- `AI/LSTM/` — LSTMMemory; `AI/InputVectorizer.cs` — векторизация наблюдений; `AI/FoodObject.cs`, `AI/EntityAction.cs`

### `AI/EntityManager.cs`
Спавн, смерть, размножение (обычное + crossover), эволюция весов (`EvolveSharedWeights`), сохранение моделей (`NeuralModelStorage.cs`, `ModelInfo.cs`).

### AISpeech (`AI/AISpeech/`) — «речь» агентов
Генетические аудио-профили: `AgentAudioProfile`, `GeneticAudioProfile` (+Cache), `AgentAudioProfileBuilder`, `TimbreResolver`, `AudioParameterMapper`; синтез: `StringToAudioGenerator` (hash строки → гармоники → Jobs → AudioClip) + `AudioSynthesizer` (Burst `IJobParallelFor`, 4 формы волны) + `StableHashService` (детерминированный хеш → ADSR огибающая).

---

## Инвентарь (`Model/InventoryModel/`, `UI/Inventory/`)

- `InventoryModel` — чистая модель (слоты, стаки, крафт)
- `EventDrivenInventoryProxy` — обёртка с событиями (Added/Removed/Changed)
- `UIInventory : ISetAble` — View, подписан на PlaceEvent/PickUpEvent из EventBus игрока
- `CraftService` (`UI/CraftService.cs`) — проверяет рецепты, заполняет прогресс-бар; `CraftPresenter` — MVP-слой; `CraftModel.cs` — мёртвый код
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

## Boids (стаи птиц/животных) — `EntitySystem/Job/`

- `BoidsBehaviour` — Jobs-система: `InitSpatialGridJob` (hash grid) + `AccelerationJob` (separation/alignment/cohesion) + `MoveJob`
- `NativeParallelMultiHashMap<int2, int>` — пространственная решётка для поиска соседей
- Настраивается через `BoidsInfo : ScriptableObject`

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
- **Test/** — `MarketSimulator`, `PriceChart`, `PointDraw` и пр. — экспериментальный/мёртвый код (кандидаты на вынос в отдельную asmdef или удаление)

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

## Известные проблемы (аудит 2026-08, по приоритету)

1. **Двойной тик сущностей**: `EntitySystem.FixedUpdate` и `EntityManager.EntityTickLoopAsync` оба крутят `UpdateEntityCycle` — нужен один драйвер.
2. **PoolManager не пулит**: `Reuse/Deactivate` сразу возвращают объект в очередь — нет разделения free/busy.
3. **Спавн-рассинхрон**: `EntitySystem.CreateMob` ищет ViewModel через `GetComponentInChildren`, `MobNAL.RunLifecycle` — через `GetComponent` на корне; при расхождении сущность не регистрируется.
4. **Сеть**: авторизация в `NetworkSpawner.IsAuthorizedClient` не работает (на сервере LocalClientId=0 — проверка бессмысленна).
5. **Утечки**: подписки R3 в `AEntityViewModel.SetupSubscriptions` не в DisposableBag; `CommandScheduler.ResetCancellation` не диспоузит старый CTS; вечные async-лупы; очистка сервисов в `GameInitializer.OnDestroy` закомментирована.
6. **AI**: `lr` умножается на знаковый reward в `DelayedPerceptron.Train` (отрицательная награда инвертирует градиент); `EvolveSharedWeights` использует AverageEntropy как fitness; `new DelayedItem` каждый тик (нужен кольцевой буфер).
7. **Мёртвый код**: `RavineNetworkManager.cs`, `RpcTest.cs`, `CraftModel.cs`, `PlayerUIService.cs`, папка `Test/`.
