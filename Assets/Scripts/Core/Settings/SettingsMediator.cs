using R3;
using System;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class SettingsMediator : IDisposable
    {
        private readonly GlobalSettingsRepository globalSettingsRepository;
        private readonly WorldConfigRepository worldConfigRepository;
        private readonly WorldRegistry worldRegistry;
        private readonly IRavineLogger _logger;

        private readonly ReactiveProperty<GlobalSettings> globalSettings;
        private readonly ReactiveProperty<WorldConfiguration> worldConfiguration;
        private readonly CompositeDisposable disposables = new();

        public ReadOnlyReactiveProperty<GlobalSettings> Global { get; }
        public ReadOnlyReactiveProperty<WorldConfiguration> WorldConfig { get; }

        private string _currentEditingWorldId;

        public SettingsMediator(
            GlobalSettingsRepository globalRepo,
            WorldConfigRepository worldConfigRepo,
            WorldRegistry registry,
            IRavineLogger logger)
        {
            globalSettingsRepository = globalRepo;
            worldConfigRepository = worldConfigRepo;
            worldRegistry = registry;
            _logger = logger;

            globalSettings = new ReactiveProperty<GlobalSettings>(new GlobalSettings());
            worldConfiguration = new ReactiveProperty<WorldConfiguration>(new WorldConfiguration());

            Global = globalSettings.ToReadOnlyReactiveProperty();
            WorldConfig = worldConfiguration.ToReadOnlyReactiveProperty();

            SubscribeToChanges();
            LoadInitialAsync().Forget();
        }
        public async UniTask LoadWorldConfigAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return;
            try
            {
                _currentEditingWorldId = worldId;
                WorldConfiguration config = await worldConfigRepository.LoadAsync(worldId);
                worldConfiguration.Value = config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки конфигурации мира {worldId}: {ex.Message}");
                worldConfiguration.Value = new WorldConfiguration { worldName = worldId };
            }
        }

        private async UniTask SaveWorldDelayedAsync()
        {
            var worldId = _currentEditingWorldId ?? worldRegistry.CurrentWorldName;
            if (string.IsNullOrEmpty(worldId)) return;

            try
            {
                await worldConfigRepository.SaveAsync(worldId, worldConfiguration.Value);
                _logger.LogInfo($"Конфигурация мира {worldId} сохранена");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения конфигурации мира: {ex.Message}");
            }
        }

        private void SubscribeToChanges()
        {
            globalSettings
                .Skip(1)
                .Subscribe(_ => SaveGlobalDelayedAsync().Forget())
                .AddTo(disposables);

            worldConfiguration
                .Skip(1)
                .Subscribe(_ => SaveWorldDelayedAsync().Forget())
                .AddTo(disposables);
        }

        public void UpdateGlobal(Action<GlobalSettings> modifier)
        {
            if (modifier == null) return;

            var settings = globalSettings.Value.Clone();
            modifier(settings);
            globalSettings.Value = settings;

        }

        public void UpdateWorldConfig(Action<WorldConfiguration> modifier)
        {
            if (modifier == null) return;

            var settings = worldConfiguration.Value.Clone();
            modifier(settings);
            worldConfiguration.Value = settings;
        }

        public async UniTask ResetToDefaultsAsync()
        {
            globalSettings.Value = new GlobalSettings();
            worldConfiguration.Value = new WorldConfiguration();

            try
            {
                await globalSettingsRepository.SaveAsync(globalSettings.Value);
                _logger.LogInfo("Настройки сброшены");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сброса: {ex.Message}");
            }
        }
        public void ChangeWorldName(string newWorldName)
        {
            worldRegistry.RenameWorldAsync(_currentEditingWorldId, newWorldName).Forget();
        }

        private async UniTask LoadInitialAsync()
        {
            try
            {
                if (await globalSettingsRepository.ExistsAsync())
                {
                    var settings = await globalSettingsRepository.LoadAsync();
                    globalSettings.Value = settings;
                }

                if (!string.IsNullOrEmpty(worldRegistry.CurrentWorldName))
                {
                    await LoadWorldConfigAsync(worldRegistry.CurrentWorldName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка начальной загрузки: {ex.Message}");
            }
        }
        private async UniTask SaveGlobalDelayedAsync()
        {
                await globalSettingsRepository.SaveAsync(globalSettings.Value);

        }

        public void Dispose()
        {
            disposables?.Dispose();
            globalSettings?.Dispose();
            worldConfiguration?.Dispose();
        }
    }
}