using System;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;


namespace TheRavine.Base
{
    public class WorldDataService : IWorldDataService, IDisposable
    {
        private readonly ReactiveProperty<WorldData> _worldData;
        private readonly IWorldManager _worldManager;
        private readonly CompositeDisposable _disposables = new();
        private IDisposable _autosaveSubscription;

        public Observable<WorldData> WorldData { get; }
        private ILogger logger; 
        public WorldDataService(IWorldManager worldManager, ILogger logger)
        {
            this.logger = logger;
            _worldManager = worldManager;
            _worldData = new ReactiveProperty<WorldData>(new WorldData());
            WorldData = _worldData.AsObservable();
            
            StartAutosave(30);
        }

        public void UpdateAutosaveInterval(int intervalSeconds)
        {
            _autosaveSubscription?.Dispose();
            
            if (intervalSeconds > 0)
            {
                StartAutosave(intervalSeconds);
            }
        }

        private void StartAutosave(int intervalSeconds)
        {
            _autosaveSubscription = Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))
                .Subscribe(_ => SaveWorldDataAsync().Forget())
                .AddTo(_disposables);
        }

        public async UniTask<bool> SaveWorldDataAsync()
        {
            if (string.IsNullOrEmpty(_worldManager.CurrentWorldName))
                return false;

            try
            {
                await UniTask.SwitchToMainThread();

                var data = _worldData.Value;
                data.lastSaveTime = System.DateTimeOffset.Now.ToUnixTimeSeconds();
                _worldData.Value = data;
                SaveLoad.SaveEncryptedData(_worldManager.CurrentWorldName, data, true);
                
                await UniTask.SwitchToThreadPool();
                
                return true;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Ошибка сохранения мира: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> LoadWorldDataAsync(string worldName)
        {
            try
            {
                if (SaveLoad.FileExists(worldName))
                {
                    var data = SaveLoad.LoadEncryptedData<WorldData>(worldName, true);
                    _worldData.Value = data;
                    return true;
                }
                _worldData.Value = new WorldData { seed = UnityEngine.Random.Range(0, int.MaxValue) };
                return true;
            }
            catch (System.Exception ex)
            {
                logger.LogError($"Ошибка загрузки мира: {ex.Message}");
                return false;
            }
        }
        public void UpdateWorldData(WorldData data)
        {
            _worldData.Value = data;
        }

        public void UpdatePlayerPosition(Vector3 position)
        {
            var data = _worldData.Value;
            data.playerPosition = position;
            _worldData.Value = data;
        }

        public void IncrementCycle()
        {
            var data = _worldData.Value;
            data.cycleCount++;
            _worldData.Value = data;
        }

        public void SetGameWon(bool won)
        {
            var data = _worldData.Value;
            data.gameWon = won;
            _worldData.Value = data;
        }

        public void Dispose()
        {
            _autosaveSubscription?.Dispose();
            _disposables?.Dispose();
            _worldData?.Dispose();
        }
    }
}