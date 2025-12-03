using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<string, WorldInfo> _worldInfoCache = new();
        private readonly ReactiveProperty<long> _cacheVersion = new(0);

        public ObservableList<string> AvailableWorlds { get; }
        public string CurrentWorldName => _currentWorld.Value;
        public Observable<string> CurrentWorld { get; }
        public Observable<bool> IsLoading { get; }
        public Observable<long> CacheVersion => _cacheVersion.AsObservable();

        public WorldRegistry(WorldStorage worldService, IRavineLogger logger)
        {
            worldStorage = worldService;
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
            
                var worldData = new WorldState
                {
                    seed = UnityEngine.Random.Range(0, int.MaxValue),
                    lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                var worldSettings = customSettings?.Clone() ?? new WorldConfiguration();
                worldSettings.worldName = worldName;
                worldSettings.createdTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                worldSettings.lastModifiedTime = worldSettings.createdTime;
                worldSettings.Validate();

                await worldStorage.SaveFullAsync(worldName, worldData, worldSettings);

                AvailableWorlds.Add(worldName);
                InvalidateCache(worldName);
                
                _logger.LogInfo($"Мир '{worldName}' создан успешно");
                return true;
        }

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
                var worldDataService = ServiceLocator.GetService<WorldStatePersistence>();
                var settingsModel = ServiceLocator.GetService<SettingsMediator>();
                
                var (worldData, worldSettings) = await worldStorage.LoadFullAsync(worldName);
            
                await worldDataService.LoadAsync(worldName);
                await settingsModel.LoadWorldConfigAsync(worldName);
                
                _currentWorld.Value = worldName;

                SceneLaunchService sceneLaunchService = ServiceLocator.GetService<SceneLaunchService>();

                if(sceneLaunchService.CanLaunch)
                {
                    sceneLaunchService.LaunchGame().Forget();
                    _logger.LogInfo($"Мир '{worldName}' загружен успешно");
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
                RemoveFromCache(worldName);
                
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
                
                RemoveFromCache(oldName);
                InvalidateCache(newName);
                
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

        public async UniTask<bool> DuplicateWorldAsync(string sourceName, string newName)
        {
            if (!AvailableWorlds.Contains(sourceName) || 
                string.IsNullOrWhiteSpace(newName) || 
                AvailableWorlds.Contains(newName))
            {
                _logger.LogWarning($"Невозможно дублировать мир {sourceName} -> {newName}");
                return false;
            }

            _isLoading.Value = true;
            
            try
            {
                var (sourceData, sourceSettings) = await worldStorage.LoadFullAsync(sourceName);
                
                var newData = sourceData.Clone();
                newData.seed = UnityEngine.Random.Range(0, int.MaxValue);
                newData.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                var newSettings = sourceSettings.Clone();
                newSettings.worldName = newName;
                newSettings.createdTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                newSettings.lastModifiedTime = newSettings.createdTime;
                
                await worldStorage.SaveFullAsync(newName, newData, newSettings);
                
                AvailableWorlds.Add(newName);
                InvalidateCache(newName);
                
                _logger.LogInfo($"Мир '{sourceName}' скопирован как '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка копирования мира '{sourceName}' -> '{newName}': {ex.Message}");
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
                ClearCache();
                
                foreach (var world in validWorlds.OrderBy(x => x))
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

        public async UniTask<WorldInfo> GetWorldInfoAsync(string worldName, bool useCache = true)
        {
            if (!AvailableWorlds.Contains(worldName))
                return null;

            if (useCache && _worldInfoCache.TryGetValue(worldName, out var cachedInfo))
                return cachedInfo;

            try
            {
                var (worldData, worldSettings) = await worldStorage.LoadFullAsync(worldName);

                var info = new WorldInfo
                {
                    Name = worldName,
                    Seed = worldData.seed,
                    LastSaveTime = DateTimeOffset.FromUnixTimeSeconds(worldData.lastSaveTime),
                    CreatedTime = DateTimeOffset.FromUnixTimeSeconds(worldSettings.createdTime),
                    CycleCount = worldData.cycleCount,
                    IsGameWon = worldData.gameWon,
                    Settings = worldSettings,
                    IsCurrentWorld = worldName == _currentWorld.Value
                };

                _worldInfoCache[worldName] = info;
                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка получения информации о мире '{worldName}': {ex.Message}");
                return null;
            }
        }

        public async UniTask<List<WorldInfo>> GetAllWorldsInfoAsync(bool useCache = true)
        {
            var worldsInfo = new List<WorldInfo>();
            
            var tasks = AvailableWorlds.Select(world => GetWorldInfoAsync(world, useCache));
            var results = await UniTask.WhenAll(tasks);
            
            foreach (var info in results)
            {
                if (info != null)
                    worldsInfo.Add(info);
            }
            
            return worldsInfo.OrderByDescending(x => x.LastSaveTime).ToList();
        }

        private void InvalidateCache(string worldName)
        {
            _worldInfoCache.Remove(worldName);
            _cacheVersion.Value++;
        }

        private void RemoveFromCache(string worldName)
        {
            _worldInfoCache.Remove(worldName);
            _cacheVersion.Value++;
        }

        private void ClearCache()
        {
            _worldInfoCache.Clear();
            _cacheVersion.Value++;
        }

        public void Dispose()
        {
            disposables?.Dispose();
            _currentWorld?.Dispose();
            _isLoading?.Dispose();
            _cacheVersion?.Dispose();
            AvailableWorlds?.Clear();
            _worldInfoCache?.Clear();
        }

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await worldStorage.ExistsAsync(worldId);
        }
    }
    [Serializable]
    public class WorldInfo
    {
        public string Name { get; set; }
        public int Seed { get; set; }
        public DateTimeOffset LastSaveTime { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public int CycleCount { get; set; }
        public bool IsGameWon { get; set; }
        public WorldConfiguration Settings { get; set; }
        public bool IsCurrentWorld { get; set; }
        
        public string GetDisplayName()
        {
            return IsCurrentWorld ? $"{Name} (Текущий)" : Name;
        }
        
        public string GetLastSaveText()
        {
            var now = DateTimeOffset.Now;
            var diff = now - LastSaveTime;
            
            return diff.TotalMinutes < 1 ? "Только что" :
                   diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes} мин назад" :
                   diff.TotalDays < 1 ? $"{(int)diff.TotalHours} ч назад" :
                   diff.TotalDays < 30 ? $"{(int)diff.TotalDays} дн назад" :
                   LastSaveTime.ToString("dd.MM.yyyy");
        }
    }
}