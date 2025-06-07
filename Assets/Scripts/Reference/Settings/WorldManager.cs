using System;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;


namespace TheRavine.Base
{
    public class WorldManager : IWorldManager, IDisposable
    {
        private readonly ReactiveProperty<string> _currentWorld;
        private readonly ObservableList<string> _availableWorlds;
        private readonly CompositeDisposable _disposables = new();

        public string CurrentWorldName => _currentWorld.Value;
        public Observable<string> CurrentWorld { get; }
        public ISynchronizedViewList<string> AvailableWorlds { get; }

        public WorldManager()
        {
            _currentWorld = new ReactiveProperty<string>();
            _availableWorlds = new ObservableList<string>();
            
            CurrentWorld = _currentWorld.AsObservable();
            AvailableWorlds = _availableWorlds.ToViewList();
            
            RefreshWorldListAsync().Forget();
        }

        public async UniTask<bool> CreateWorldAsync(string worldName)
        {
            if (string.IsNullOrWhiteSpace(worldName) || _availableWorlds.Contains(worldName))
                return false;

            try
            {
                var worldData = new WorldData
                {
                    seed = UnityEngine.Random.Range(0, int.MaxValue),
                    lastSaveTime = System.DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                await UniTask.SwitchToThreadPool();
                SaveLoad.SaveEncryptedData(worldName, worldData);
                await UniTask.SwitchToMainThread();

                _availableWorlds.Add(worldName);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка создания мира: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> LoadWorldAsync(string worldName)
        {
            if (!_availableWorlds.Contains(worldName))
                return false;

            var worldDataService = ServiceLocator.Get<IWorldDataService>();
            if (await worldDataService.LoadWorldDataAsync(worldName))
            {
                _currentWorld.Value = worldName;
                
                var settingsModel = ServiceLocator.Get<ISettingsModel>();
                var worldSettings = LoadWorldSettings(worldName);
                settingsModel.UpdateWorldSettings(worldSettings);
                
                return true;
            }
            
            return false;
        }

        public async UniTask<bool> DeleteWorldAsync(string worldName)
        {
            if (!_availableWorlds.Contains(worldName))
                return false;

            try
            {
                await UniTask.SwitchToThreadPool();
                SaveLoad.DeleteFile(worldName);
                SaveLoad.DeleteFile($"{worldName}_world_settings");
                await UniTask.SwitchToMainThread();

                _availableWorlds.Remove(worldName);
                
                if (_currentWorld.Value == worldName)
                    _currentWorld.Value = null;
                    
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка удаления мира: {ex.Message}");
                return false;
            }
        }

        public async UniTask RefreshWorldListAsync()
        {
            try
            {
                await UniTask.SwitchToThreadPool();
                var worlds = SaveLoad.GetAllWorldNames(); // Предполагаем, что этот метод существует
                await UniTask.SwitchToMainThread();
                
                _availableWorlds.Clear();
                foreach (var world in worlds)
                    _availableWorlds.Add(world);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка обновления списка миров: {ex.Message}");
            }
        }

        private WorldSettings LoadWorldSettings(string worldName)
        {
            var settingsFile = $"{worldName}_world_settings";
            if (SaveLoad.FileExists(settingsFile))
                return SaveLoad.LoadEncryptedData<WorldSettings>(settingsFile);
            return new WorldSettings { worldName = worldName };
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _currentWorld?.Dispose();
            _availableWorlds?.Clear();
        }
    }
}