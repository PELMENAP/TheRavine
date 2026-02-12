using System;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public class WorldStatePersistence : IDisposable
    {
        private readonly WorldStateRepository worldStateRepository;
        private readonly WorldRegistry worldRegistry;
        private readonly IRavineLogger _logger;

        private readonly ReactiveProperty<WorldState> worldState;
        private readonly AutosaveSystem autosaveSystem;
        private readonly CompositeDisposable disposables = new();

        public ReadOnlyReactiveProperty<WorldState> State { get; }

        public WorldStatePersistence(
            WorldStateRepository repo,
            WorldRegistry registry,
            AutosaveSystem _autosaveSystem,
            IRavineLogger logger)
        {
            worldStateRepository = repo;
            worldRegistry = registry;
            autosaveSystem = _autosaveSystem;
            _logger = logger;

            worldState = new ReactiveProperty<WorldState>(new WorldState());
            State = worldState.ToReadOnlyReactiveProperty();


            autosaveSystem.AddSaveAction(SaveAsync);
            worldState
                .Skip(1)
                .Subscribe(_ => autosaveSystem.MarkDirty())
                .AddTo(disposables);
        }

        public void UpdateState(Action<WorldState> modifier)
        {
            if (modifier == null) return;

            var state = worldState.Value;
            modifier(state);
            worldState.Value = state;
            worldState.ForceNotify();
        }

        public void SetAutosaveInterval(int seconds) 
            => autosaveSystem.SetInterval(seconds);

        public async UniTask<bool> LoadAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("Попытка загрузки с пустым ID");
                return false;
            }

            try
            {
                WorldState state;

                if (await worldStateRepository.ExistsAsync(worldId))
                {
                    state = await worldStateRepository.LoadAsync(worldId);
                    _logger.LogInfo($"Состояние мира {worldId} загружено");
                }
                else
                {
                    state = new WorldState
                    {
                        seed = UnityEngine.Random.Range(0, int.MaxValue),
                        lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                    };
                    _logger.LogInfo($"Создано новое состояние для {worldId}");
                }

                worldState.Value = state;
                autosaveSystem.MarkClean();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> SaveAsync()
        {
            var worldId = worldRegistry.CurrentWorldName;
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("Попытка сохранения без активного мира");
                return false;
            }

            try
            {
                var state = worldState.Value;
                state.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await worldStateRepository.SaveAsync(worldId, state);

                _logger.LogInfo($"Состояние мира {worldId} сохранено");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            disposables?.Dispose();
            worldState?.Dispose();
        }
    }
}