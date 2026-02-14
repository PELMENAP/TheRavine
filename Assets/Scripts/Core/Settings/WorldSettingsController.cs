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
                        _config.Value = new WorldConfiguration();
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
                var config = _worldRegistry.GetCurrentConfig();
                _config.Value = config;
                
                _autosave.SetInterval(config.autosaveInterval);
                
                _logger.LogInfo("[WorldSettings] Настройки мира загружены");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldSettings] Ошибка загрузки: {ex.Message}");
            }
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
            
            var updatedConfig = _worldRegistry.GetCurrentConfig();
            _config.Value = updatedConfig;

            if (updatedConfig.autosaveInterval != _autosave.IntervalSeconds)
            {
                _autosave.SetInterval(updatedConfig.autosaveInterval);
            }

            SaveAsync().Forget();
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
            if (!_worldRegistry.HasLoadedWorld)
            {
                _logger.LogWarning("[WorldSettings] Нет загруженного мира");
                return false;
            }

            var currentId = _worldRegistry.CurrentWorldId.CurrentValue;
            var success = await _worldRegistry.RenameWorldAsync(currentId, newName);

            if (success)
            {
                var config = _worldRegistry.GetCurrentConfig();
                _config.Value = config;
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