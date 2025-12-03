using R3;
using System;
using System.Threading;
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

        private CancellationTokenSource globalSaveCts, worldSaveCts;
        private const float SaveDebounceTime = 0.5f;

        public ReadOnlyReactiveProperty<GlobalSettings> Global { get; }
        public ReadOnlyReactiveProperty<WorldConfiguration> WorldConfig { get; }

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

        private void SubscribeToChanges()
        {
            globalSettings
                .Skip(1)
                .Subscribe(_ => DebouncedSaveGlobal())
                .AddTo(disposables);

            worldConfiguration
                .Skip(1)
                .Subscribe(_ => DebouncedSaveWorld())
                .AddTo(disposables);
        }

        public void UpdateGlobal(Action<GlobalSettings> modifier)
        {
            if (modifier == null) return;

            var settings = globalSettings.Value;
            modifier(settings);
            globalSettings.ForceNotify();
        }

        public void UpdateWorldConfig(Action<WorldConfiguration> modifier)
        {
            if (modifier == null) return;

            var settings = worldConfiguration.Value;
            modifier(settings);
            worldConfiguration.ForceNotify();
        }

        public async UniTask LoadWorldConfigAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return;

            try
            {
                WorldConfiguration config = await worldConfigRepository.LoadAsync(worldId);
                worldConfiguration.Value = config;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки конфигурации мира {worldId}: {ex.Message}");
                worldConfiguration.Value = new WorldConfiguration { worldName = worldId };
            }
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

        private void DebouncedSaveGlobal()
        {
            globalSaveCts?.Cancel();
            globalSaveCts = new CancellationTokenSource();
            SaveGlobalDelayedAsync(globalSaveCts.Token).Forget();
        }

        private void DebouncedSaveWorld()
        {
            worldSaveCts?.Cancel();
            worldSaveCts = new CancellationTokenSource();
            SaveWorldDelayedAsync(worldSaveCts.Token).Forget();
        }

        private async UniTask SaveGlobalDelayedAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SaveDebounceTime), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            try
            {
                await globalSettingsRepository.SaveAsync(globalSettings.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения глобальных настроек: {ex.Message}");
            }
        }

        private async UniTask SaveWorldDelayedAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SaveDebounceTime), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            var worldId = worldRegistry.CurrentWorldName;
            if (string.IsNullOrEmpty(worldId)) return;

            try
            {
                await worldConfigRepository.SaveAsync(worldId, worldConfiguration.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения конфигурации мира: {ex.Message}");
            }
        }

        public void Dispose()
        {
            globalSaveCts?.Cancel();
            globalSaveCts?.Dispose();
            worldSaveCts?.Cancel();
            worldSaveCts?.Dispose();
            disposables?.Dispose();
            globalSettings?.Dispose();
            worldConfiguration?.Dispose();
        }
    }
}