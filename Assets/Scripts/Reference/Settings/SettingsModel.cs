using R3;
using System;

namespace TheRavine.Base
{
    public class SettingsModel : ISettingsModel, IDisposable
    {
        private readonly ReactiveProperty<GameSettings> _gameSettings;
        private readonly ReactiveProperty<WorldSettings> _worldSettings;
        private readonly CompositeDisposable _disposables = new();

        public ReadOnlyReactiveProperty<GameSettings> GameSettings { get; }
        public ReadOnlyReactiveProperty<WorldSettings> WorldSettings { get; }
        
        public SettingsModel()
        {
            _gameSettings = new ReactiveProperty<GameSettings>(LoadGameSettings());
            _worldSettings = new ReactiveProperty<WorldSettings>(LoadWorldSettings());
            
            GameSettings = _gameSettings.ToReadOnlyReactiveProperty();
            WorldSettings = _worldSettings.ToReadOnlyReactiveProperty();
            
            _gameSettings.Skip(1).Subscribe(SaveGameSettings).AddTo(_disposables);
            _worldSettings.Skip(1).Subscribe(SaveWorldSettings).AddTo(_disposables);
        }

        public void UpdateGameSettings(GameSettings settings)
        {
            _gameSettings.Value = settings.Clone();
        }

        public void UpdateWorldSettings(WorldSettings settings)
        {
            _worldSettings.Value = settings.Clone();
        }

        public void ResetToDefaults()
        {
            _gameSettings.Value = new GameSettings();
            _worldSettings.Value = new WorldSettings();
        }

        private GameSettings LoadGameSettings()
        {
            if (SaveLoad.FileExists("game_settings"))
                return SaveLoad.LoadEncryptedData<GameSettings>("game_settings", true);
            return new GameSettings();
        }

        private WorldSettings LoadWorldSettings()
        {
            var worldManager = ServiceLocator.Get<IWorldManager>();
            if (worldManager?.CurrentWorldName != null && SaveLoad.FileExists(worldManager.CurrentWorldName, true))
                return SaveLoad.LoadEncryptedData<WorldSettings>(worldManager.CurrentWorldName, true);
            return new WorldSettings();
        }

        private void SaveGameSettings(GameSettings settings)
        {
            SaveLoad.SaveEncryptedData("game_settings", settings, true);
        }

        private void SaveWorldSettings(WorldSettings settings)
        {
            var worldManager = ServiceLocator.Get<IWorldManager>();
            if (worldManager?.CurrentWorldName != null)
                SaveLoad.SaveEncryptedData(worldManager.CurrentWorldName, settings, true);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _gameSettings?.Dispose();
            _worldSettings?.Dispose();
        }
    }
}