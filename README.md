# TheRavine — Project Architecture Summary

## Stack
- **Unity** (3D, URP), **C#**, **Netcode for GameObjects** (host/server/client)
- **UniTask** — async/await everywhere instead of coroutines
- **R3** — reactive properties, observables (аналог UniRx)
- **LitMotion** — tweens/animations
- **MemoryPack** — binary serialization для сохранений
- **ZString / Cysharp.Text** — zero-alloc string building
- **ZLinq** — zero-alloc LINQ
- **Unity.Jobs + Burst** — высоконагруженные вычисления
- **LLMUnity** — локальная LLM для описаний предметов
- **NaughtyAttributes** — editor utilities

---

## Точка входа и жизненный цикл

### `GameInitializer` (DontDestroyOnLoad, Awake)
Создаёт все глобальные сервисы **до** загрузки игровой сцены:
- `RavineLogger` → `ServiceLocator.Services`
- `ActionMapController`
- `GlobalSettingsController` (настройки качества, трава, управление)
- `WorldRegistry` (список миров, текущий мир)
- `AutosaveCoordinator`
- `WorldSettingsController`

### Главное меню — `MenuMainScript`
- `WorldManagerUI` — список миров, создание/удаление/переименование
- `WorldSettingsUI` — конфиг мира (сложность, autosave interval, timeScale)
- `NetworkUIController` — выбор host/server/client, IP
- `SceneLaunchService` — переход на игровую сцену

### Игровая сцена — `Bootstrap` → `GameStateMachine`
Состояния: `BootstrapState → InitialState → LoadingState → GameState`  
Каждое состояние запускает очередь `ISetAble` сервисов через `ServiceRegisterMachine`.

**Порядок регистрации сервисов (грубо):**
1. Bootstrap: `DayCycle`, `AmbientSystem`
2. Initial: `ObjectSystem`, `EntitySystem`, `MobController`
3. Loading: `MapGenerator`, `MobGenerator`, `UIInventory`, `PauseUI`

---

## ServiceLocator
```
ServiceLocator.Services  — Dictionary<Type, object>  — игровые сервисы
ServiceLocator.Players   — PlayerContainer            — зарегистрированные игроки
```
**Проблема**: `Register<T>` молча возвращает `false` если тип уже есть → при повторном входе в мир сервисы не перерегистрируются. Нужно вызывать `ServiceLocator.ClearAll()` при выходе в меню.

---

## Entity System (компонентная архитектура)

### `AEntity` — базовый класс сущности
```
Dictionary<Type, IComponent>  — компоненты
ReactiveProperty<bool> IsActive
ReactiveCommand<Unit> OnUpdate
```

### Ключевые компоненты (`IComponent`)
| Компонент | Назначение |
|-----------|-----------|
| `MainComponent` | имя, prefabID, clientID |
| `TransformComponent` | entity/model трансформы |
| `MovementComponent` | BaseSpeed, Acceleration, Deceleration, velocity |
| `EnergyComponent` | energy, maxEnergy (ReactiveProperty) |
| `CurrencyComponent` | валюта (SafeInt + ReactiveProperty, только сервер пишет) |
| `AimComponent` | CrosshairDistance, PickDistance |
| `EventBusComponent` | EventBus для внутренних событий сущности |
| `StatePatternComponent` | FSM — текущее состояние (AState) |
| `SkillComponent` | Dictionary<string, ISkill> |
| `CameraComponent` | логика камеры (follow + aim offset) |

### Конкретные сущности
- **`PlayerEntity`** — игрок. Init в `PlayerModelView.OnNetworkSpawn()`
- **`BotEntity`** — NPC с FSM-поведениями
- **`MobEntity`** — мобы, движение через `RoamMoveController` + `MobController` (Jobs)

### MVP-слой
- `AEntityViewModel : NetworkBehaviour` — подписывается на `Entity.IsActive`, `Entity.OnUpdate`
- `AEntityView : MonoBehaviour` — биндинги UI на ViewModel
- `PlayerModelView` — конкретная ViewModel игрока, спавнит камеру через ServerRpc

---

## Сеть (Netcode for GameObjects)
- Архитектура: **Host = Server + Client**
- `PlayerModelView.OnNetworkSpawn` — инициализация на каждом клиенте
- `DayCycle` — NetworkVariable<bool> isDay, сервер управляет временем
- `CurrencyNetworkComponent` — NetworkVariable<int>, только сервер пишет, RPC от клиента
- `PlayerController.MoveServerRpc` — движение валидируется на сервере

---

## Сохранения

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

### Данные
- **`GlobalSettings`** — качество, трава, тип управления (MemoryPack)
- **`WorldState`** — seed, позиция игрока, cycleCount, инвентарь[], lastSaveTime
- **`WorldConfiguration`** — имя мира, сложность, autosave interval, timeScale

### `WorldRegistry`
Центральный менеджер миров: Create/Load/Unload/Delete/Rename/Save.  
`AvailableWorlds: ObservableList<string>` — UI подписывается напрямую.

### `AutosaveCoordinator`
Запускает цикл автосохранения. Поддерживает `SubscribeBeforeSave(Func<CT, UniTask>)` — хуки до сохранения.

---

## Генерация мира

### `MapGenerator : MonoBehaviour, ISetAble`
- Chunk-based, infinite. ChunkSize=40, Scale=2 (юниты на тайл)
- Шум Перлина через статический `Noise` (кешированные октавные офсеты)
- `ChunkData` = heightMap[40,40] + temperatureMap[40,40] + SortedSet<Vector2Int> objectsToInst
- Обновление по позиции игрока раз в секунду

### Бесконечные слои (`IEndless`)
- `EndlessTerrain` — обновляет единый Mesh (3x3 чанка), MeshCollider
- `EndlessLiquids` — двигает водный plane
- `EndlessObjects` — переиспользует объекты через `ObjectSystem` (pool)

### `ObjectSystem`
- `Dictionary<Vector2Int, ObjectInstInfo>` — глобальный реестр размещённых объектов
- `PoolManager` — Dictionary<prefabID, Queue<ObjectInstance>>
- `ObjectInfoRegistry : ScriptableObject` — база всех объектов

### NAL (Natural Artificial Life) — `NAL_PC`
Псевдо-экосистема: объекты размножаются/умирают по вероятностям.  
BehaviourType: `None | NAL | GROW`  
При смерти/подборе — `SpreadPattern` (spawn новых объектов вокруг)

### WFC (Wave Function Collapse) — `WaveFunctionCollapseAlgorithm`
Процедурная генерация структур из тайловых правил (`TileRuleSO`).

---

## AI-система (Entity2D / SharedHierarchicalBrain)

Отдельная sandbox-система (не связана с основным EntitySystem):

### `SharedHierarchicalBrain`
Двухуровневая иерархия:
1. **Coordinator** (LSTM + DelayedPerceptron) — выбирает Goal: Survive/Hunt/Forage/Social
2. **Executor[goal]** (LSTM + DelayedPerceptron) — выбирает конкретное действие из подмножества

### `DelayedPerceptron`
- Continuous-time RNN с τ-весами (время релаксации)
- Обучение: Truncated BPTT (8 шагов назад), reward-weighted
- Exploration: ε-greedy с adaptive epsilon по entropy
- Генетические параметры (`GeneticParameters`): LR, lambda, temperature, etc.

### `EntityManager`
Спавн, смерть, размножение (обычное + crossover), эволюция весов (`EvolveSharedWeights`).

---

## Инвентарь

- `InventoryModel` — чистая модель (слоты, стаки, крафт)
- `EventDrivenInventoryProxy` — обёртка с событиями (Added/Removed/Changed)
- `UIInventory : ISetAble` — View, подписан на PlaceEvent/PickUpEvent из EventBus игрока
- `CraftService` — проверяет рецепты, заполняет прогресс-бар
- `UIDragger` — drag-and-drop (PC мышь + mobile touch)
- **LLM-описания**: `LLMItemDescriptionService` с debounce (0.4s), кеш в `ItemDescriptionRegistry`

---

## Таймеры

- `TimeInvoker : MonoBehaviour` (DontDestroyOnLoad) — **проблема**: использует `UniTask.Delay(1/60s)` вместо `Update()`, `Time.deltaTime` читается в неправильный момент
- `SyncedTimer` — подписывается на события TimeInvoker, поддерживает Pause/Unpause

---

## Терминал / Скриптовый язык Rive

- `Terminal : MonoBehaviour` — UI консоль с историей (50 строк, ZString)
- `CommandManager` — Dictionary команд по имени/псевдониму
- **Rive** — собственный интерпретируемый язык:
  - Типы: `int`, массивы `int[]`
  - Конструкции: `if/else/end`, `for x = N to M`, `wait`, `log`, `get` (stdin), `send(interactor, val)`
  - `~команда` — вызов терминальных команд из скрипта
  - `RiveParser` → AST → `RiveExecutor` (stack-based scopes, MAX 10000 ops)
  - `RiveRuntime` — реестр программ, built-in функции (abs/min/max/clamp/pow/sqrt/rand)
  - `InteractorRegistry` — игровые головоломки (DigitalLock, Collatz, Checksum, SequenceValidator)
- `GestureRecognizer` — PDollar+ распознавание жестов → диспетчер команд

---

## Управление вводом

- `ActionMapController` — переключение Action Maps: Gameplay / Pause / Inventory / UI
- `InputBindingAdapter` — связывает `Button.onClick` + `InputAction.performed` в одном объекте
- `IController` — абстракция над `PCController` (мышь+клавиатура) и `JoistickController`
- `DoubleTapDetector` — детектор двойного нажатия для буста

---

## Звук

- `StringToAudioGenerator` — синтез звука из строки (hash → гармоники → Jobs → AudioClip)
- `AudioSynthesizer` — `IJobParallelFor` с BurstCompile, 4 формы волны (Sine/Saw/Square/Triangle)
- `StableHashService` — детерминированный хеш строки → ADSR огибающая + гармоники

---

## Boids (стаи птиц/животных)

- `BoidsBehaviour` — Jobs-система: `InitSpatialGridJob` (hash grid) + `AccelerationJob` (separation/alignment/cohesion) + `MoveJob`
- `NativeParallelMultiHashMap<int2, int>` — пространственная решётка для поиска соседей
- Настраивается через `BoidsInfo : ScriptableObject`

---

## Ключевые паттерны

| Паттерн | Где |
|---------|-----|
| Service Locator | `ServiceLocator` (глобальный) |
| Component | `AEntity` + `IComponent` |
| State Machine | `StatePatternComponent` + `AState` + FSM игры |
| Command | `ICommand` (EntitySystem) + `ICommand` (Terminal) — разные интерфейсы! |
| Repository | `WorldStateRepository`, `WorldConfigRepository`, etc. |
| Observer / Reactive | R3 `ReactiveProperty`, `Subject`, `Observable` |
| Object Pool | `PoolManager` |
| MVVM (частично) | `AEntityViewModel` + `AEntityView` |

