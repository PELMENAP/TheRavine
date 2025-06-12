using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;

namespace TheRavine.Base
{
    public class WorldManager : IWorldManager, IDisposable
    {
        private readonly ReactiveProperty<string> _currentWorld;
        private readonly ReactiveProperty<bool> _isLoading;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<string, WorldSettings> _worldSettingsCache = new();

        public ObservableList<string> AvailableWorlds { get; }
        public string CurrentWorldName => _currentWorld.Value;
        public Observable<string> CurrentWorld { get; }
        public Observable<bool> IsLoading { get; }
        
        private ILogger logger;
        public WorldManager(ILogger logger)
        {
            _worldSettingsCache.Clear();
            this.logger = logger;
            _currentWorld = new ReactiveProperty<string>();
            AvailableWorlds = new ObservableList<string>();
            _isLoading = new ReactiveProperty<bool>(false);
            
            CurrentWorld = _currentWorld.AsObservable();
            IsLoading = _isLoading.AsObservable();
            
            RefreshWorldListAsync().Forget();
        }

        public async UniTask<bool> CreateWorldAsync(string worldName, WorldSettings customSettings = null)
        {
            if (string.IsNullOrWhiteSpace(worldName) || AvailableWorlds.Contains(worldName))
                return false;

            _isLoading.Value = true;
            
            try
            {
                var worldData = new WorldData
                {
                    seed = UnityEngine.Random.Range(0, int.MaxValue),
                    lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                var worldSettings = customSettings?.Clone() ?? new WorldSettings();
                worldSettings.worldName = worldName;
                worldSettings.Validate();

                SaveLoad.SaveEncryptedData(worldName, worldData);
                SaveLoad.SaveEncryptedData(worldName, worldSettings, true);

                AvailableWorlds.Add(worldName);
                _worldSettingsCache[worldName] = worldSettings;
                
                logger.LogInfo($"Мир '{worldName}' создан успешно");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка создания мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> LoadWorldAsync(string worldName)
        {
            if (!AvailableWorlds.Contains(worldName))
                return false;

            _isLoading.Value = true;
            
            try
            {
                var worldDataService = ServiceLocator.Get<IWorldDataService>();
                if (await worldDataService.LoadWorldDataAsync(worldName))
                {
                    _currentWorld.Value = worldName;
                    
                    var worldSettings = await LoadWorldSettingsAsync(worldName);
                    var settingsModel = ServiceLocator.Get<ISettingsModel>();
                    settingsModel.UpdateWorldSettings(worldSettings);
                    
                    logger.LogInfo($"Мир '{worldName}' загружен успешно");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка загрузки мира '{worldName}': {ex.Message}");
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
                return false;

            _isLoading.Value = true;
            
            try
            {
                SaveLoad.DeleteFile(worldName);
                SaveLoad.DeleteFile(worldName, true);
                
                AvailableWorlds.Remove(worldName);
                _worldSettingsCache.Remove(worldName);
                
                if (_currentWorld.Value == worldName)
                    _currentWorld.Value = null;
                
                logger.LogInfo($"Мир '{worldName}' удален успешно");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка удаления мира '{worldName}': {ex.Message}");
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
                return false;

            _isLoading.Value = true;
            
            try
            {
                var worldData = SaveLoad.LoadEncryptedData<WorldData>(oldName);
                var worldSettings = SaveLoad.LoadEncryptedData<WorldSettings>(oldName, true);
                
                worldSettings.worldName = newName;
                worldSettings.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                SaveLoad.SaveEncryptedData(newName, worldData);
                SaveLoad.SaveEncryptedData(newName, worldSettings, true);
                
                SaveLoad.DeleteFile(oldName);
                SaveLoad.DeleteFile(oldName, true);
                
                await UniTask.SwitchToMainThread();

                var index = AvailableWorlds.IndexOf(oldName);
                AvailableWorlds.RemoveAt(index);
                AvailableWorlds.Insert(index, newName);
                
                _worldSettingsCache.Remove(oldName);
                _worldSettingsCache[newName] = worldSettings;
                
                if (_currentWorld.Value == oldName)
                    _currentWorld.Value = newName;
                
                logger.LogInfo($"Мир переименован с '{oldName}' на '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка переименования мира '{oldName}' -> '{newName}': {ex.Message}");
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
                return false;

            _isLoading.Value = true;
            
            try
            {
                await UniTask.SwitchToThreadPool();
                
                var worldData = SaveLoad.LoadEncryptedData<WorldData>(sourceName);
                var worldSettings = SaveLoad.LoadEncryptedData<WorldSettings>(sourceName, true);
                
                worldData.seed = UnityEngine.Random.Range(0, int.MaxValue); // Новый сид
                worldData.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                
                worldSettings.worldName = newName;
                worldSettings.createdTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                worldSettings.lastModifiedTime = worldSettings.createdTime;
                
                SaveLoad.SaveEncryptedData(newName, worldData);
                SaveLoad.SaveEncryptedData(newName, worldSettings, true);
                
                await UniTask.SwitchToMainThread();

                AvailableWorlds.Add(newName);
                _worldSettingsCache[newName] = worldSettings;
                
                logger.LogInfo($"Мир '{sourceName}' скопирован как '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка копирования мира '{sourceName}' -> '{newName}': {ex.Message}");
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
            
                var worlds = SaveLoad.GetAllWorldNames();
                var validWorlds = new List<string>();
                
                foreach (var world in worlds)
                {
                    try
                    {
                        if (SaveLoad.FileExists(world))
                        {
                            SaveLoad.LoadEncryptedData<WorldData>(world);
                            validWorlds.Add(world);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Поврежденный мир '{world}' исключен из списка: {ex.Message}");
                    }
                }
                
                AvailableWorlds.Clear();
                _worldSettingsCache.Clear();
                
                foreach (var world in validWorlds.OrderBy(x => x))
                {
                    AvailableWorlds.Add(world);
                }
                
                logger.LogInfo($"Обновлен список миров: найдено {validWorlds.Count} валидных миров");
        }

        public async UniTask<WorldSettings> LoadWorldSettingsAsync(string worldName)
        {
            if (_worldSettingsCache.TryGetValue(worldName, out var cachedSettings))
                return cachedSettings;

            try
            {
                
                var settingsFile = worldName;
                WorldSettings settings;
                
                if (SaveLoad.FileExists(settingsFile))
                {
                    settings = SaveLoad.LoadEncryptedData<WorldSettings>(settingsFile, true);
                }
                else
                {
                    settings = new WorldSettings { worldName = worldName };
                    SaveLoad.SaveEncryptedData(settingsFile, settings, true);
                }
                
                settings.Validate();
                _worldSettingsCache[worldName] = settings;
                
                return settings;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка загрузки настроек мира '{worldName}': {ex.Message}");
                return new WorldSettings { worldName = worldName };
            }
        }

        public async UniTask<bool> SaveWorldSettingsAsync(string worldName, WorldSettings settings)
        {
            if (string.IsNullOrEmpty(worldName) || settings == null)
                return false;

            try
            {
                settings.worldName = worldName;
                settings.Validate();
                
                await UniTask.SwitchToThreadPool();
                
                SaveLoad.SaveEncryptedData(worldName, settings, true);
                
                await UniTask.SwitchToMainThread();
                
                _worldSettingsCache[worldName] = settings;
                
                logger.LogInfo($"Настройки мира '{worldName}' сохранены");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка сохранения настроек мира '{worldName}': {ex.Message}");
                return false;
            }
        }

        public WorldInfo GetWorldInfo(string worldName)
        {
            if (!AvailableWorlds.Contains(worldName))
                return null;

            try
            {
                var worldData = SaveLoad.LoadEncryptedData<WorldData>(worldName);
                var worldSettings = _worldSettingsCache.ContainsKey(worldName) 
                    ? _worldSettingsCache[worldName] 
                    : LoadWorldSettingsAsync(worldName).GetAwaiter().GetResult();

                return new WorldInfo
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
            }
            catch (Exception ex)
            {
                logger.LogError($"Ошибка получения информации о мире '{worldName}': {ex.Message}");
                return null;
            }
        }

        public List<WorldInfo> GetAllWorldsInfo()
        {
            var worldsInfo = new List<WorldInfo>();
            
            foreach (var worldName in AvailableWorlds)
            {
                var info = GetWorldInfo(worldName);
                if (info != null)
                    worldsInfo.Add(info);
            }
            
            return worldsInfo.OrderByDescending(x => x.LastSaveTime).ToList();
        }
        public void Dispose()
        {
            _disposables?.Dispose();
            _currentWorld?.Dispose();
            _isLoading?.Dispose();
            AvailableWorlds?.Clear();
            _worldSettingsCache?.Clear();
        }
    }

    [System.Serializable]
    public class WorldInfo
    {
        public string Name { get; set; }
        public int Seed { get; set; }
        public DateTimeOffset LastSaveTime { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public int CycleCount { get; set; }
        public bool IsGameWon { get; set; }
        public WorldSettings Settings { get; set; }
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