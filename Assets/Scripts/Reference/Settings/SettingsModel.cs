using R3;
using System;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class SettingsModel : IDisposable
    {
        private readonly GameSettingsManager _gameSettingsManager;
        private readonly WorldManager _worldManager;
        private readonly WorldService _worldService;
        private readonly ReactiveProperty<GameSettings> _gameSettings;
        private readonly ReactiveProperty<WorldSettings> _worldSettings;
        private readonly CompositeDisposable _disposables = new();
        private readonly IRavineLogger _logger;

        public ReadOnlyReactiveProperty<GameSettings> GameSettings { get; }
        public ReadOnlyReactiveProperty<WorldSettings> WorldSettings { get; }
        
        public SettingsModel(
            GameSettingsManager gameSettingsManager,
            WorldManager worldManager,
            WorldService worldService,
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
                .Subscribe(settings => SaveGameSettingsAsync(settings).Forget())
                .AddTo(_disposables);
            
            _worldSettings
                .Skip(1)
                .Subscribe(settings => SaveWorldSettingsAsync(settings).Forget())
                .AddTo(_disposables);
            
            LoadInitialSettingsAsync().Forget();
        }

        public void UpdateGameSettings(GameSettings settings)
        {
            if (settings == null) return;
            _gameSettings.Value = settings.Clone();
        }

        public void UpdateWorldSettings(WorldSettings settings)
        {
            if (settings == null) return;
            _worldSettings.Value = settings.Clone();
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

        private async UniTask SaveGameSettingsAsync(GameSettings settings)
        {
            try
            {
                await _gameSettingsManager.SaveAsync(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения игровых настроек: {ex.Message}");
            }
        }

        private async UniTask SaveWorldSettingsAsync(WorldSettings settings)
        {
            var currentWorld = _worldManager.CurrentWorldName;
            if (string.IsNullOrEmpty(currentWorld)) return;
            
            try
            {
                await _worldService.SaveSettingsAsync(currentWorld, settings);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения настроек мира {currentWorld}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _gameSettings?.Dispose();
            _worldSettings?.Dispose();
        }
    }
}