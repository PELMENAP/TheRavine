using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;
using ZLinq;

namespace TheRavine.Base
{
    public class WorldRegistry : IDisposable
    {
        private readonly WorldStorage storage;
        private readonly RavineLogger logger;
        
        private readonly ReactiveProperty<string> currentWorldId;
        private readonly ReactiveProperty<bool> isLoading;
        private readonly CompositeDisposable disposables = new();

        private WorldState currentState;
        private WorldConfiguration currentConfig;
        private bool hasUnsavedChanges;

        public ObservableList<string> AvailableWorlds { get; }
        public ReadOnlyReactiveProperty<string> CurrentWorldId { get; }
        public ReadOnlyReactiveProperty<bool> IsLoading { get; }

        public bool HasLoadedWorld => !string.IsNullOrEmpty(currentWorldId.Value);
        public string CurrentWorldName => currentConfig?.worldName ?? currentWorldId.Value;

        public WorldRegistry(
            IAsyncPersistentStorage persistenceStorage,
            RavineLogger logger)
        {
            WorldStateRepository worldStateRepo = new(persistenceStorage);
            WorldConfigRepository worldConfigRepo = new(persistenceStorage);
            storage = new WorldStorage(worldStateRepo, worldConfigRepo);
            this.logger = logger;
            
            currentWorldId = new ReactiveProperty<string>();
            isLoading = new ReactiveProperty<bool>(false);
            AvailableWorlds = new ObservableList<string>();

            CurrentWorldId = currentWorldId.ToReadOnlyReactiveProperty();
            IsLoading = isLoading.ToReadOnlyReactiveProperty();

            RefreshWorldListAsync().Forget();
        }

        public async UniTask<bool> CreateWorldAsync(string worldName, WorldConfiguration customConfig = null)
        {
            if (string.IsNullOrWhiteSpace(worldName))
            {
                logger.LogWarning("[WorldRegistry] Пустое имя мира");
                return false;
            }

            if (AvailableWorlds.Contains(worldName))
            {
                logger.LogWarning($"[WorldRegistry] Мир '{worldName}' уже существует");
                return false;
            }

            isLoading.Value = true;

            try
            {
                var state = new WorldState
                {
                    seed = UnityEngine.Random.Range(0, int.MaxValue),
                    lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                var config = customConfig?.Clone() ?? new WorldConfiguration();
                config.worldName = worldName;
                config.createdTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                config.lastModifiedTime = config.createdTime;
                config.Validate();

                await storage.SaveFullAsync(worldName, state, config);
                AvailableWorlds.Add(worldName);

                logger.LogInfo($"[WorldRegistry] Мир '{worldName}' создан");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Ошибка создания мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                isLoading.Value = false;
            }
        }

        public async UniTask<bool> LoadWorldAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                logger.LogWarning("[WorldRegistry] Попытка загрузки с пустым worldId");
                return false;
            }

            if (!AvailableWorlds.Contains(worldId))
            {
                logger.LogWarning($"[WorldRegistry] Мир '{worldId}' не найден");
                return false;
            }

            if (hasUnsavedChanges)
            {
                await SaveCurrentWorldAsync();
            }

            isLoading.Value = true;

            try
            {
                var (state, config) = await storage.LoadFullAsync(worldId);
                
                currentState = state;
                currentConfig = config;
                hasUnsavedChanges = false;

                currentWorldId.Value = worldId;

                logger.LogInfo($"[WorldRegistry] Мир '{worldId}' загружен");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Ошибка загрузки '{worldId}': {ex.Message}");
                return false;
            }
            finally
            {
                isLoading.Value = false;
            }
        }

        public async UniTask<bool> UnloadWorldAsync()
        {
            if (!HasLoadedWorld) return true;

            if (hasUnsavedChanges)
            {
                await SaveCurrentWorldAsync();
            }

            currentWorldId.Value = null;
            currentState = default;
            currentConfig = null;
            hasUnsavedChanges = false;

            logger.LogInfo("[WorldRegistry] Мир выгружен");
            return true;
        }

        public async UniTask<bool> DeleteWorldAsync(string worldId)
        {
            if (!AvailableWorlds.Contains(worldId))
            {
                logger.LogWarning($"[WorldRegistry] Мир '{worldId}' не существует");
                return false;
            }

            isLoading.Value = true;

            try
            {
                if (currentWorldId.Value == worldId)
                {
                    await UnloadWorldAsync();
                }

                await storage.DeleteAsync(worldId);
                AvailableWorlds.Remove(worldId);

                logger.LogInfo($"[WorldRegistry] Мир '{worldId}' удалён");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Ошибка удаления '{worldId}': {ex.Message}");
                return false;
            }
            finally
            {
                isLoading.Value = false;
            }
        }

        public async UniTask<bool> RenameWorldAsync(string oldName, string newName)
        {
            if (!AvailableWorlds.Contains(oldName))
            {
                logger.LogWarning($"[WorldRegistry] Мир '{oldName}' не найден");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                logger.LogWarning("[WorldRegistry] Новое имя пустое");
                return false;
            }

            if (AvailableWorlds.Contains(newName))
            {
                logger.LogWarning($"[WorldRegistry] Имя '{newName}' уже занято");
                return false;
            }

            isLoading.Value = true;

            try
            {
                var (state, config) = await storage.LoadFullAsync(oldName);
                
                config.worldName = newName;
                config.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await storage.SaveFullAsync(newName, state, config);
                await storage.DeleteAsync(oldName);

                var index = AvailableWorlds.IndexOf(oldName);
                AvailableWorlds.RemoveAt(index);
                AvailableWorlds.Insert(index, newName);

                if (currentWorldId.Value == oldName)
                {
                    currentWorldId.Value = newName;
                    currentConfig = config;
                }

                logger.LogInfo($"[WorldRegistry] Мир переименован: '{oldName}' → '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Ошибка переименования: {ex.Message}");
                return false;
            }
            finally
            {
                isLoading.Value = false;
            }
        }

        public WorldState GetCurrentState()
        {
            if (!HasLoadedWorld)
                throw new InvalidOperationException("Нет загруженного мира");

            return currentState.Clone();
        }

        public WorldConfiguration GetCurrentConfig()
        {
            if (!HasLoadedWorld)
                throw new InvalidOperationException("Нет загруженного мира");

            return currentConfig.Clone();
        }

        public void UpdateState(Action<WorldState> modifier)
        {
            if (!HasLoadedWorld)
            {
                logger.LogWarning("[WorldRegistry] Попытка обновления без загруженного мира");
                return;
            }

            modifier(currentState);
            hasUnsavedChanges = true;
        }

        public void UpdateConfig(Action<WorldConfiguration> modifier)
        {
            if (!HasLoadedWorld)
            {
                logger.LogWarning("[WorldRegistry] Попытка обновления без загруженного мира");
                return;
            }

            modifier(currentConfig);
            currentConfig.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            currentConfig.Validate();
            hasUnsavedChanges = true;
        }

        public async UniTask<bool> SaveCurrentWorldAsync()
        {
            if (!HasLoadedWorld)
            {
                logger.LogWarning("[WorldRegistry] Нет загруженного мира для сохранения");
                return false;
            }

            try
            {
                currentState.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await storage.SaveFullAsync(currentWorldId.Value, currentState, currentConfig);
                hasUnsavedChanges = false;
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"[WorldRegistry] Ошибка сохранения: {ex.Message}");
                return false;
            }
        }

        public async UniTask<(WorldState state, WorldConfiguration config)> LoadWorldDataAsync(string worldId)
        {
            return await storage.LoadFullAsync(worldId);
        }

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await storage.ExistsAsync(worldId);
        }

        public async UniTask RefreshWorldListAsync()
        {
            isLoading.Value = true;

            try
            {
                var worldIds = await storage.GetAllWorldIdsAsync();
                var validWorlds = new List<string>();

                foreach (var worldId in worldIds)
                {
                    if (await storage.ExistsAsync(worldId))
                    {
                        validWorlds.Add(worldId);
                    }
                }

                AvailableWorlds.Clear();
                foreach (var worldId in validWorlds.AsValueEnumerable().OrderBy(x => x))
                {
                    AvailableWorlds.Add(worldId);
                }

                logger.LogInfo($"[WorldRegistry] Найдено миров: {validWorlds.Count}");
            }
            finally
            {
                isLoading.Value = false;
            }
        }

        public void Dispose()
        {
            disposables?.Dispose();
            currentWorldId?.Dispose();
            isLoading?.Dispose();
            AvailableWorlds?.Clear();
        }
    }
}