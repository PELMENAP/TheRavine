using R3;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class SettingsModel : IDisposable
    {
        private readonly GameSettingsManager _gameSettingsManager;
        private readonly WorldManager _worldManager;
        private readonly WorldFileService _worldService;
        private readonly ReactiveProperty<GameSettings> _gameSettings;
        private readonly ReactiveProperty<WorldSettings> _worldSettings;
        private readonly CompositeDisposable _disposables = new();
        private readonly IRavineLogger _logger;

        private CancellationTokenSource _saveCts;
        private const float SaveDebounceTime = 0.5f;

        public ReadOnlyReactiveProperty<GameSettings> GameSettings { get; }
        public ReadOnlyReactiveProperty<WorldSettings> WorldSettings { get; }
        
        public SettingsModel(
            GameSettingsManager gameSettingsManager,
            WorldManager worldManager,
            WorldFileService worldService,
            IRavineLogger logger)
        {
            _gameSettingsManager = gameSettingsManager;
            _worldManager = worldManager;
            _worldService = worldService;
            _logger = logger;
            
            _gameSettings = new ReactiveProperty<GameSettings>(new GameSettings());
            _worldSettings = new ReactiveProperty<WorldSettings>(new WorldSettings());
            
            GameSettings = _gameSettings.ToReadOnlyReactiveProperty();
            WorldSettings = _worldSettings.ToReadOnlyReactiveProperty();
            
            _gameSettings
                .Skip(1)
                .Subscribe(_ => DebouncedSaveGameSettings())
                .AddTo(_disposables);
            
            _worldSettings
                .Skip(1)
                .Subscribe(_ => DebouncedSaveWorldSettings())
                .AddTo(_disposables);
            
            LoadInitialSettingsAsync().Forget();
        }

        public void ModifyGameSettings(Action<GameSettings> modifier)
        {
            if (modifier == null) return;
            
            var settings = _gameSettings.Value;
            modifier(settings);
            _gameSettings.ForceNotify();
        }

        public void ModifyWorldSettings(Action<WorldSettings> modifier)
        {
            if (modifier == null) return;
            
            var settings = _worldSettings.Value;
            modifier(settings);
            _worldSettings.ForceNotify();
        }

        public async UniTask ResetToDefaultsAsync()
        {
            _gameSettings.Value = new GameSettings();
            _worldSettings.Value = new WorldSettings();
            
            try
            {
                await _gameSettingsManager.SaveAsync(_gameSettings.Value);
                _logger.LogInfo("Настройки сброшены к значениям по умолчанию");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сброса настроек: {ex.Message}");
            }
        }

        public async UniTask LoadWorldSettingsAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return;
            
            try
            {
                var settings = await _worldService.LoadSettingsAsync(worldId);
                _worldSettings.Value = settings;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки настроек мира {worldId}: {ex.Message}");
                _worldSettings.Value = new WorldSettings();
            }
        }

        private async UniTask LoadInitialSettingsAsync()
        {
            try
            {
                if (await _gameSettingsManager.ExistsAsync())
                {
                    var gameSettings = await _gameSettingsManager.LoadAsync();
                    _gameSettings.Value = gameSettings;
                }
                
                if (!string.IsNullOrEmpty(_worldManager.CurrentWorldName))
                {
                    await LoadWorldSettingsAsync(_worldManager.CurrentWorldName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки начальных настроек: {ex.Message}");
            }
        }

        private void DebouncedSaveGameSettings()
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            SaveGameSettingsDelayed(_saveCts.Token).Forget();
        }

        private void DebouncedSaveWorldSettings()
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            SaveWorldSettingsDelayed(_saveCts.Token).Forget();
        }

        private async UniTask SaveGameSettingsDelayed(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SaveDebounceTime), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            try
            {
                await _gameSettingsManager.SaveAsync(_gameSettings.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения игровых настроек: {ex.Message}");
            }
        }

        private async UniTask SaveWorldSettingsDelayed(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SaveDebounceTime), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            var currentWorld = _worldManager.CurrentWorldName;
            if (string.IsNullOrEmpty(currentWorld)) return;
            
            try
            {
                await _worldService.SaveSettingsAsync(currentWorld, _worldSettings.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения настроек мира {currentWorld}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _disposables?.Dispose();
            _gameSettings?.Dispose();
            _worldSettings?.Dispose();
        }
    }
}