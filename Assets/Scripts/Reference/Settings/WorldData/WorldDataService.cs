using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace TheRavine.Base
{
    public class WorldDataService : IDisposable
    {
        private readonly ReactiveProperty<WorldData> _worldData;
        private readonly WorldManager _worldManager;
        private readonly WorldFileService _worldService;
        private readonly IRavineLogger _logger;
        private readonly CompositeDisposable _disposables = new();
        
        private IDisposable _autosaveSubscription;
        private bool _isDirty = false;

        public Observable<WorldData> WorldDataObserver { get; }
        public Observable<bool> IsDirty { get; }

        public ReadOnlyReactiveProperty<WorldData> WorldData => _worldData;

        public WorldDataService(
            WorldManager worldManager,
            WorldFileService worldService,
            IRavineLogger logger)
        {
            _worldManager = worldManager;
            _worldService = worldService;
            _logger = logger;

            _worldData = new ReactiveProperty<WorldData>(new WorldData());
            WorldDataObserver = _worldData.AsObservable();

            var isDirtyProperty = new ReactiveProperty<bool>(false);
            IsDirty = isDirtyProperty.AsObservable();

            _worldData
                .Skip(1)
                .Subscribe(_ =>
                {
                    _isDirty = true;
                    isDirtyProperty.Value = true;
                })
                .AddTo(_disposables);

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
                .Where(_ => _isDirty)
                .Subscribe(_ =>
                {
                    SaveWorldDataAsync()
                        .ContinueWith(result =>
                        {
                            if (result) _logger.LogInfo("Автосохранение выполнено");
                        }).Forget();
                })
                .AddTo(_disposables);
        }

        public async UniTask<bool> SaveWorldDataAsync()
        {
            var currentWorld = _worldManager.CurrentWorldName;
            if (string.IsNullOrEmpty(currentWorld))
            {
                _logger.LogWarning("Попытка сохранения данных мира без активного мира");
                return false;
            }

            try
            {
                var data = _worldData.Value;
                data.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                await _worldService.SaveDataAsync(currentWorld, data);
                
                _isDirty = false;
                _logger.LogInfo($"Данные мира {currentWorld} сохранены");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка сохранения данных мира {currentWorld}: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> LoadWorldDataAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("Попытка загрузки данных мира с пустым ID");
                return false;
            }

            try
            {
                WorldData data;
                
                if (await _worldService.ExistsAsync(worldId))
                {
                    data = await _worldService.LoadDataAsync(worldId);
                    _logger.LogInfo($"Данные мира {worldId} загружены");
                }
                else
                {
                    data = new WorldData 
                    { 
                        seed = UnityEngine.Random.Range(0, int.MaxValue),
                        lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                    };
                    _logger.LogInfo($"Созданы новые данные для мира {worldId}");
                }
                
                _worldData.Value = data;
                _isDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки данных мира {worldId}: {ex.Message}");
                return false;
            }
        }

        public void UpdateWorldData(WorldData data)
        {
            if (data.IsDefault())
            {
                _logger.LogWarning("Попытка обновления данных мира с null значением");
                return;
            }
            
            _worldData.Value = data;
        }

        public void UpdatePlayerPosition(Vector3 position)
        {
            var data = _worldData.Value;
            if (data.playerPosition != position)
            {
                data.playerPosition = position;
                _worldData.Value = data;
            }
        }

        public void IncrementCycle()
        {
            var data = _worldData.Value;
            data.cycleCount++;
            _worldData.Value = data;
            _logger.LogInfo($"Цикл увеличен до {data.cycleCount}");
        }
        
        public void SetTime(float time)
        {
            var data = _worldData.Value;
            data.startTime = time; 
            _worldData.Value = data;
            _logger.LogInfo($"Цикл поставлен на значение: {data.startTime}");
        }

        public void SetGameWon(bool won)
        {
            var data = _worldData.Value;
            if (data.gameWon != won)
            {
                data.gameWon = won;
                _worldData.Value = data;
                _logger.LogInfo($"Статус победы изменен на {won}");
            }
        }

        public async UniTask ForceUpdateSeed()
        {
            var data = _worldData.Value;
            data.seed = UnityEngine.Random.Range(0, int.MaxValue);
            _worldData.Value = data;
            
            await SaveWorldDataAsync();
        }

        public void Dispose()
        {
            if (_isDirty)
            {
                SaveWorldDataAsync().Forget();
            }
            
            _autosaveSubscription?.Dispose();
            _disposables?.Dispose();
            _worldData?.Dispose();
        }
    }
}