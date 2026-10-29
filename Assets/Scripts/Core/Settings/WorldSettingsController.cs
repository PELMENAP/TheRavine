using System;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public class WorldSettingsController : IDisposable
    {
        private readonly WorldRegistry _worldRegistry;
        private readonly AutosaveCoordinator _autosave;
        private readonly RavineLogger _logger;
        private readonly ReactiveProperty<WorldConfiguration> _config;
        private readonly CompositeDisposable _disposables = new();

        public ReadOnlyReactiveProperty<WorldConfiguration> Config { get; }

        public WorldSettingsController(
            WorldRegistry worldRegistry,
            AutosaveCoordinator autosave,
            RavineLogger logger)
        {
            _worldRegistry = worldRegistry;
            _autosave = autosave;
            _logger = logger;

            _config = new ReactiveProperty<WorldConfiguration>(new WorldConfiguration());
            Config = _config.ToReadOnlyReactiveProperty();

            SubscribeToWorldChanges();
        }

        private void SubscribeToWorldChanges()
        {
            _worldRegistry.CurrentWorldId
                .Subscribe(worldId =>
                {
                    if (string.IsNullOrEmpty(worldId))
                    {
                        Publish(new WorldConfiguration());
                    }
                    else
                    {
                        LoadCurrentWorldConfigAsync().Forget();
                    }
                })
                .AddTo(_disposables);
        }

        private async UniTaskVoid LoadCurrentWorldConfigAsync()
        {
            try
            {
                Publish(_worldRegistry.GetCurrentConfig());
                
                _logger.LogInfo("[WorldSettings] Настройки мира загружены");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldSettings] Ошибка загрузки: {ex.Message}");
            }

            await UniTask.CompletedTask;
        }

        public void Update(Action<WorldConfiguration> modifier)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[WorldSettings] Null модификатор");
                return;
            }

            if (!_worldRegistry.HasLoadedWorld)
            {
                _logger.LogWarning("[WorldSettings] Нет загруженного мира");
                return;
            }

            _worldRegistry.UpdateConfig(modifier);
            
            Publish(_worldRegistry.GetCurrentConfig());

            SaveAsync().Forget();
        }

        private void Publish(WorldConfiguration config)
        {
            _config.Value = config;
            SimulationClock.TimeScale = config.timeScale;

            if (_autosave.IntervalSeconds != config.autosaveInterval)
            {
                _autosave.SetInterval(config.autosaveInterval);
            }
        }

        public async UniTask<bool> SaveAsync()
        {
            try
            {
                return await _worldRegistry.SaveCurrentWorldAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldSettings] Ошибка сохранения: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> RenameWorldAsync(string newName)
        {
            if (!WorldRegistry.IsValidWorldName(newName))
            {
                _logger.LogWarning($"[WorldSettings] Недопустимое имя мира: '{newName}'");
                return false;
            }

            if (!_worldRegistry.HasLoadedWorld)
            {
                _logger.LogWarning("[WorldSettings] Нет загруженного мира");
                return false;
            }

            var currentId = _worldRegistry.CurrentWorldId.CurrentValue;
            var success = await _worldRegistry.RenameWorldAsync(currentId, newName);

            if (success)
            {
                Publish(_worldRegistry.GetCurrentConfig());
            }

            return success;
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _config?.Dispose();
        }
    }
}