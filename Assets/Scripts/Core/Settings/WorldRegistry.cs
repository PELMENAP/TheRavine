using System;
using System.Collections.Generic;
using ZLinq;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;

namespace TheRavine.Base
{
    public class WorldRegistry : IDisposable
    {
        private readonly WorldStorage worldStorage;
        private readonly IRavineLogger _logger;
        private readonly ReactiveProperty<string> _currentWorld;
        private readonly ReactiveProperty<bool> _isLoading;
        private readonly CompositeDisposable disposables = new();

        public ObservableList<string> AvailableWorlds { get; }
        public string CurrentWorldName => _currentWorld.Value;
        public Observable<string> CurrentWorld { get; }
        public Observable<bool> IsLoading { get; }

        public WorldRegistry(WorldStateRepository worldStateRepository, WorldConfigRepository worldConfigRepository, IRavineLogger logger)
        {
            worldStorage = new(worldStateRepository, worldConfigRepository);
            _logger = logger;
            
            _currentWorld = new ReactiveProperty<string>();
            _isLoading = new ReactiveProperty<bool>(false);
            AvailableWorlds = new ObservableList<string>();
            
            CurrentWorld = _currentWorld.AsObservable();
            IsLoading = _isLoading.AsObservable();
            
            RefreshWorldListAsync().Forget();
        }

        public async UniTask<bool> CreateWorldAsync(string worldName, WorldConfiguration customSettings = null)
        {
            if (string.IsNullOrWhiteSpace(worldName))
            {
                _logger.LogWarning("Попытка создания мира с пустым именем");
                return false;
            }
            
            if (AvailableWorlds.Contains(worldName))
            {
                _logger.LogWarning($"Мир {worldName} уже существует");
                return false;
            }

            _isLoading.Value = true;
            
            try
            {
                var worldState = new WorldState
                {
                    seed = UnityEngine.Random.Range(0, int.MaxValue),
                    lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                var worldConfiguration = customSettings?.Clone() ?? new WorldConfiguration();
                worldConfiguration.worldName = worldName;
                worldConfiguration.createdTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                worldConfiguration.lastModifiedTime = worldConfiguration.createdTime;
                worldConfiguration.Validate();

                await worldStorage.SaveFullAsync(worldName, worldState, worldConfiguration);

                AvailableWorlds.Add(worldName);
                
                _logger.LogInfo($"Мир '{worldName}' создан успешно");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка создания мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }
        public async UniTask<(WorldState, WorldConfiguration)> LoadCurrentWorldFullAsync() => await worldStorage.LoadFullAsync(CurrentWorldName);
        public async UniTask<(WorldState, WorldConfiguration)> LoadFullAsync(string worldName) => await worldStorage.LoadFullAsync(worldName);
        public async UniTask<WorldState> LoadDataAsync() => await worldStorage.LoadDataAsync(CurrentWorldName);
        public async UniTask SaveDataAsync(WorldState data) => await worldStorage.SaveDataAsync(CurrentWorldName, data);
        public async UniTask<bool> LoadWorldAsync(string worldName)
        {
            if (string.IsNullOrEmpty(worldName) || !AvailableWorlds.Contains(worldName))
            {
                _logger.LogWarning($"Попытка загрузки несуществующего мира: {worldName}");
                return false;
            }

            _isLoading.Value = true;
            
            try
            {
                SceneLaunchService sceneLaunchService = ServiceLocator.GetService<SceneLaunchService>();

                if(sceneLaunchService.CanLaunch)
                {
                    _currentWorld.Value = worldName;
                    await sceneLaunchService.LaunchGame();
                    _logger.LogInfo($"Мир '{worldName}' загружен успешно");
                    ServiceLocator.GetService<AutosaveSystem>().Start();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> DeleteWorldAsync(string worldName)
        {
            if (!AvailableWorlds.Contains(worldName))
            {
                _logger.LogWarning($"Попытка удаления несуществующего мира: {worldName}");
                return false;
            }

            _isLoading.Value = true;
            
            try
            {
                await worldStorage.DeleteAsync(worldName);
                
                AvailableWorlds.Remove(worldName);
                
                if (_currentWorld.Value == worldName)
                    _currentWorld.Value = null;
                
                _logger.LogInfo($"Мир '{worldName}' удален успешно");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка удаления мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> RenameWorldAsync(string oldName, string newName)
        {
            if (!AvailableWorlds.Contains(oldName) || 
                string.IsNullOrWhiteSpace(newName) || 
                AvailableWorlds.Contains(newName))
            {
                _logger.LogWarning($"Невозможно переименовать мир {oldName} -> {newName}");
                return false;
            }

            _isLoading.Value = true;
            
            try
            {
                var (worldData, worldSettings) = await worldStorage.LoadFullAsync(oldName);
                
                worldSettings.worldName = newName;
                worldSettings.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                await worldStorage.SaveFullAsync(newName, worldData, worldSettings);
                await worldStorage.DeleteAsync(oldName);
                
                var index = AvailableWorlds.IndexOf(oldName);
                AvailableWorlds.RemoveAt(index);
                AvailableWorlds.Insert(index, newName);
                
                if (_currentWorld.Value == oldName)
                    _currentWorld.Value = newName;
                
                _logger.LogInfo($"Мир переименован с '{oldName}' на '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка переименования мира '{oldName}' -> '{newName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask RefreshWorldListAsync()
        {
            _isLoading.Value = true;
            
            try
            {
                var worlds = await worldStorage.GetAllWorldIdsAsync();
                var validWorlds = new List<string>();
                
                foreach (var world in worlds)
                {
                    try
                    {
                        if (await worldStorage.ExistsAsync(world))
                        {
                            validWorlds.Add(world);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Поврежденный мир '{world}' исключен из списка: {ex.Message}");
                    }
                }
                
                AvailableWorlds.Clear();
                
                foreach (var world in validWorlds.AsValueEnumerable().OrderBy(x => x))
                {
                    AvailableWorlds.Add(world);
                }
                
                _logger.LogInfo($"Обновлен список миров: найдено {validWorlds.Count} валидных миров");
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await worldStorage.ExistsAsync(worldId);
        }

        public void Dispose()
        {
            disposables?.Dispose();
            _currentWorld?.Dispose();
            _isLoading?.Dispose();
            AvailableWorlds?.Clear();
        }
    }
}