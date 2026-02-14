using R3;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace TheRavine.Base
{
    public class SettingsMediator : IDisposable
    {
        private readonly GlobalSettingsRepository _globalRepo;
        private readonly WorldConfigRepository _worldConfigRepo;
        private readonly WorldRegistry _registry;
        private readonly IRavineLogger _logger;

        private readonly ReactiveProperty<GlobalSettings> _globalSettings;
        private readonly ReactiveProperty<WorldConfiguration> _worldConfig;
        private readonly CompositeDisposable _disposables = new();

        private string _loadedWorldId;
        private CancellationTokenSource _saveCts;

        public ReadOnlyReactiveProperty<GlobalSettings> Global { get; }
        public ReadOnlyReactiveProperty<WorldConfiguration> WorldConfig { get; }
        private readonly ReactiveProperty<string> _editingWorldId;
        public ReadOnlyReactiveProperty<string> EditingWorldId { get; }

        public SettingsMediator(
            GlobalSettingsRepository globalRepo,
            WorldConfigRepository worldConfigRepo,
            WorldRegistry registry,
            IRavineLogger logger)
        {
            _globalRepo = globalRepo;
            _worldConfigRepo = worldConfigRepo;
            _registry = registry;
            _logger = logger;

            _globalSettings = new ReactiveProperty<GlobalSettings>(new GlobalSettings());
            _worldConfig = new ReactiveProperty<WorldConfiguration>(new WorldConfiguration());

            Global = _globalSettings.ToReadOnlyReactiveProperty();
            WorldConfig = _worldConfig.ToReadOnlyReactiveProperty();

            _editingWorldId = new ReactiveProperty<string>();
            EditingWorldId = _editingWorldId.ToReadOnlyReactiveProperty();
            _editingWorldId
                .Where(worldId => !string.IsNullOrEmpty(worldId))
                .Subscribe(async worldId => await LoadWorldConfigForEditingAsync(worldId))
                .AddTo(_disposables);

            SubscribeToChanges();
            LoadInitialGlobalAsync().Forget();
        }

        private void SubscribeToChanges()
        {
            _globalSettings
                .Skip(1)
                .Throttle(0.5f)
                .Subscribe(_ => SaveGlobalAsync().Forget())
                .AddTo(_disposables);

            _worldConfig
                .Skip(1)
                .Where(_ => !string.IsNullOrEmpty(_loadedWorldId) || !string.IsNullOrEmpty(_editingWorldId.Value))
                .Throttle(0.5f)
                .Subscribe(_ => SaveWorldConfigAsync().Forget())
                .AddTo(_disposables);

            _registry.CurrentWorld
                .Subscribe(async worldId =>
                {
                    if (!string.IsNullOrEmpty(worldId))
                    {
                        await LoadWorldConfigAsync(worldId);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(_editingWorldId.Value))
                        {
                            UnloadWorldConfig();
                        }
                    }
                })
                .AddTo(_disposables);
        }

        private async UniTask LoadInitialGlobalAsync()
        {
            try
            {
                if (await _globalRepo.ExistsAsync())
                {
                    var settings = await _globalRepo.LoadAsync();
                    _globalSettings.Value = settings;
                    _logger.LogInfo("[Settings] Глобальные настройки загружены");
                }
                else
                {
                    _logger.LogInfo("[Settings] Используются настройки по умолчанию");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка загрузки глобальных настроек: {ex.Message}");
            }
        }

        private async UniTask LoadWorldConfigAsync(string worldId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("[Settings] Попытка загрузки конфигурации с пустым worldId");
                return;
            }

            try
            {
                WorldConfiguration config;

                if (await _worldConfigRepo.ExistsAsync(worldId))
                {
                    config = await _worldConfigRepo.LoadAsync(worldId);
                    _logger.LogInfo($"[Settings] Конфигурация мира '{worldId}' загружена");
                }
                else
                {
                    config = new WorldConfiguration { worldName = worldId };
                    _logger.LogInfo($"[Settings] Создана новая конфигурация для '{worldId}'");
                }

                if (ct.IsCancellationRequested) return;

                _loadedWorldId = worldId;
                _worldConfig.Value = config;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"[Settings] Загрузка конфигурации '{worldId}' отменена");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка загрузки конфигурации '{worldId}': {ex.Message}");
                _worldConfig.Value = new WorldConfiguration { worldName = worldId };
            }
        }

        public async UniTask StartEditingWorldAsync(string worldId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("[Settings] Попытка редактирования с пустым worldId");
                return;
            }

            _editingWorldId.Value = worldId;
        }

        public void StopEditingWorld()
        {
            _editingWorldId.Value = null;
            
            if (string.IsNullOrEmpty(_registry.CurrentWorldName))
            {
                UnloadWorldConfig();
            }
        }

        private async UniTask LoadWorldConfigForEditingAsync(string worldId, CancellationToken ct = default)
        {
            await LoadWorldConfigAsync(worldId, ct);
        }

        private async UniTask SaveWorldConfigAsync(CancellationToken ct = default)
        {
            var targetWorldId = _loadedWorldId ?? _editingWorldId.Value;
            
            if (string.IsNullOrEmpty(targetWorldId))
            {
                _logger.LogWarning("[Settings] Попытка сохранения WorldConfig без загруженного/редактируемого мира");
                return;
            }

            _saveCts?.Cancel();
            _saveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                var config = _worldConfig.Value;
                config.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                await _worldConfigRepo.SaveAsync(targetWorldId, config);
                _logger.LogInfo($"[Settings] Конфигурация мира '{targetWorldId}' сохранена");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"[Settings] Сохранение конфигурации '{targetWorldId}' отменено");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка сохранения конфигурации '{targetWorldId}': {ex.Message}");
            }
        }

        public async UniTask RenameCurrentWorldAsync(string newWorldName, CancellationToken ct = default)
        {
            var targetWorldId = _loadedWorldId ?? _editingWorldId.Value;
            
            if (string.IsNullOrEmpty(targetWorldId))
            {
                _logger.LogWarning("[Settings] Нет загруженного/редактируемого мира для переименования");
                return;
            }

            if (string.IsNullOrWhiteSpace(newWorldName))
            {
                _logger.LogWarning("[Settings] Попытка переименования в пустое имя");
                return;
            }

            try
            {
                await _registry.RenameWorldAsync(targetWorldId, newWorldName);
                
                UpdateWorldConfig(c => c.worldName = newWorldName);
                
                if (!string.IsNullOrEmpty(_editingWorldId.Value) && _editingWorldId.Value == targetWorldId)
                {
                    _editingWorldId.Value = newWorldName;
                }
                
                _logger.LogInfo($"[Settings] Мир переименован: '{targetWorldId}' -> '{newWorldName}'");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка переименования мира: {ex.Message}");
            }
        }

        private void UnloadWorldConfig()
        {
            _loadedWorldId = null;
            _worldConfig.Value = new WorldConfiguration();
            _logger.LogInfo("[Settings] Конфигурация мира выгружена");
        }

        public void UpdateGlobal(Action<GlobalSettings> modifier)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[Settings] Null модификатор для Global");
                return;
            }

            var settings = _globalSettings.Value.Clone();
            modifier(settings);
            _globalSettings.Value = settings;
        }

        public void UpdateWorldConfig(Action<WorldConfiguration> modifier)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[Settings] Null модификатор для WorldConfig");
                return;
            }

            if (string.IsNullOrEmpty(_loadedWorldId))
            {
                _logger.LogWarning("[Settings] Попытка обновления WorldConfig без загруженного мира");
                return;
            }

            var config = _worldConfig.Value.Clone();
            modifier(config);
            config.Validate();
            _worldConfig.Value = config;
        }

        public async UniTask ResetToDefaultsAsync(CancellationToken ct = default)
        {
            try
            {
                _globalSettings.Value = new GlobalSettings();
                
                if (!string.IsNullOrEmpty(_loadedWorldId))
                {
                    _worldConfig.Value = new WorldConfiguration { worldName = _loadedWorldId };
                }

                await SaveGlobalAsync(ct);
                
                _logger.LogInfo("[Settings] Настройки сброшены к значениям по умолчанию");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка сброса настроек: {ex.Message}");
            }
        }

        private async UniTask SaveGlobalAsync(CancellationToken ct = default)
        {
            _saveCts?.Cancel();
            _saveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await _globalRepo.SaveAsync(_globalSettings.Value);
                _logger.LogInfo("[Settings] Глобальные настройки сохранены");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo("[Settings] Сохранение глобальных настроек отменено");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Settings] Ошибка сохранения глобальных настроек: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            
            _disposables?.Dispose();
            _globalSettings?.Dispose();
            _worldConfig?.Dispose();
        }
    }
}