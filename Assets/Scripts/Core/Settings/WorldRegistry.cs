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
        private readonly WorldStorage _storage;
        private readonly RavineLogger _logger;
        
        private readonly ReactiveProperty<string> _currentWorldId;
        private readonly ReactiveProperty<bool> _isLoading;
        private readonly CompositeDisposable _disposables = new();

        private WorldState _currentState;
        private WorldConfiguration _currentConfig;
        private bool _hasUnsavedChanges;

        public ObservableList<string> AvailableWorlds { get; }
        public ReadOnlyReactiveProperty<string> CurrentWorldId { get; }
        public ReadOnlyReactiveProperty<bool> IsLoading { get; }

        public bool HasLoadedWorld => !string.IsNullOrEmpty(_currentWorldId.Value);
        public string CurrentWorldName => _currentConfig?.worldName ?? _currentWorldId.Value;

        public WorldRegistry(
            IAsyncPersistentStorage persistenceStorage,
            RavineLogger logger)
        {
            WorldStateRepository worldStateRepo = new(persistenceStorage);
            WorldConfigRepository worldConfigRepo = new(persistenceStorage);
            _storage = new WorldStorage(worldStateRepo, worldConfigRepo);
            _logger = logger;
            
            _currentWorldId = new ReactiveProperty<string>();
            _isLoading = new ReactiveProperty<bool>(false);
            AvailableWorlds = new ObservableList<string>();

            CurrentWorldId = _currentWorldId.ToReadOnlyReactiveProperty();
            IsLoading = _isLoading.ToReadOnlyReactiveProperty();

            RefreshWorldListAsync().Forget();
        }

        public async UniTask<bool> CreateWorldAsync(string worldName, WorldConfiguration customConfig = null)
        {
            if (string.IsNullOrWhiteSpace(worldName))
            {
                _logger.LogWarning("[WorldRegistry] Пустое имя мира");
                return false;
            }

            if (AvailableWorlds.Contains(worldName))
            {
                _logger.LogWarning($"[WorldRegistry] Мир '{worldName}' уже существует");
                return false;
            }

            _isLoading.Value = true;

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

                await _storage.SaveFullAsync(worldName, state, config);
                AvailableWorlds.Add(worldName);

                _logger.LogInfo($"[WorldRegistry] Мир '{worldName}' создан");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldRegistry] Ошибка создания мира '{worldName}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> LoadWorldAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
            {
                _logger.LogWarning("[WorldRegistry] Попытка загрузки с пустым worldId");
                return false;
            }

            if (!AvailableWorlds.Contains(worldId))
            {
                _logger.LogWarning($"[WorldRegistry] Мир '{worldId}' не найден");
                return false;
            }

            if (_hasUnsavedChanges)
            {
                await SaveCurrentWorldAsync();
            }

            _isLoading.Value = true;

            try
            {
                var (state, config) = await _storage.LoadFullAsync(worldId);
                
                _currentState = state;
                _currentConfig = config;
                _hasUnsavedChanges = false;

                _currentWorldId.Value = worldId;

                _logger.LogInfo($"[WorldRegistry] Мир '{worldId}' загружен");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldRegistry] Ошибка загрузки '{worldId}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> UnloadWorldAsync()
        {
            if (!HasLoadedWorld) return true;

            if (_hasUnsavedChanges)
            {
                await SaveCurrentWorldAsync();
            }

            _currentWorldId.Value = null;
            _currentState = default;
            _currentConfig = null;
            _hasUnsavedChanges = false;

            _logger.LogInfo("[WorldRegistry] Мир выгружен");
            return true;
        }

        public async UniTask<bool> DeleteWorldAsync(string worldId)
        {
            if (!AvailableWorlds.Contains(worldId))
            {
                _logger.LogWarning($"[WorldRegistry] Мир '{worldId}' не существует");
                return false;
            }

            _isLoading.Value = true;

            try
            {
                if (_currentWorldId.Value == worldId)
                {
                    await UnloadWorldAsync();
                }

                await _storage.DeleteAsync(worldId);
                AvailableWorlds.Remove(worldId);

                _logger.LogInfo($"[WorldRegistry] Мир '{worldId}' удалён");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldRegistry] Ошибка удаления '{worldId}': {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public async UniTask<bool> RenameWorldAsync(string oldName, string newName)
        {
            if (!AvailableWorlds.Contains(oldName))
            {
                _logger.LogWarning($"[WorldRegistry] Мир '{oldName}' не найден");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                _logger.LogWarning("[WorldRegistry] Новое имя пустое");
                return false;
            }

            if (AvailableWorlds.Contains(newName))
            {
                _logger.LogWarning($"[WorldRegistry] Имя '{newName}' уже занято");
                return false;
            }

            _isLoading.Value = true;

            try
            {
                var (state, config) = await _storage.LoadFullAsync(oldName);
                
                config.worldName = newName;
                config.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await _storage.SaveFullAsync(newName, state, config);
                await _storage.DeleteAsync(oldName);

                var index = AvailableWorlds.IndexOf(oldName);
                AvailableWorlds.RemoveAt(index);
                AvailableWorlds.Insert(index, newName);

                if (_currentWorldId.Value == oldName)
                {
                    _currentWorldId.Value = newName;
                    _currentConfig = config;
                }

                _logger.LogInfo($"[WorldRegistry] Мир переименован: '{oldName}' → '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldRegistry] Ошибка переименования: {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public WorldState GetCurrentState()
        {
            if (!HasLoadedWorld)
                throw new InvalidOperationException("Нет загруженного мира");

            return _currentState.Clone();
        }

        public WorldConfiguration GetCurrentConfig()
        {
            if (!HasLoadedWorld)
                throw new InvalidOperationException("Нет загруженного мира");

            return _currentConfig.Clone();
        }

        public void UpdateState(Action<WorldState> modifier)
        {
            if (!HasLoadedWorld)
            {
                _logger.LogWarning("[WorldRegistry] Попытка обновления без загруженного мира");
                return;
            }

            modifier(_currentState);
            _hasUnsavedChanges = true;
        }

        public void UpdateConfig(Action<WorldConfiguration> modifier)
        {
            if (!HasLoadedWorld)
            {
                _logger.LogWarning("[WorldRegistry] Попытка обновления без загруженного мира");
                return;
            }

            modifier(_currentConfig);
            _currentConfig.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            _currentConfig.Validate();
            _hasUnsavedChanges = true;
        }

        public async UniTask<bool> SaveCurrentWorldAsync()
        {
            if (!HasLoadedWorld)
            {
                _logger.LogWarning("[WorldRegistry] Нет загруженного мира для сохранения");
                return false;
            }

            try
            {
                _currentState.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await _storage.SaveFullAsync(_currentWorldId.Value, _currentState, _currentConfig);
                _hasUnsavedChanges = false;

                _logger.LogInfo($"[WorldRegistry] Мир '{_currentWorldId.Value}' сохранён");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldRegistry] Ошибка сохранения: {ex.Message}");
                return false;
            }
        }

        public async UniTask<(WorldState state, WorldConfiguration config)> LoadWorldDataAsync(string worldId)
        {
            return await _storage.LoadFullAsync(worldId);
        }

        public async UniTask<bool> ExistsAsync(string worldId)
        {
            return await _storage.ExistsAsync(worldId);
        }

        public async UniTask RefreshWorldListAsync()
        {
            _isLoading.Value = true;

            try
            {
                var worldIds = await _storage.GetAllWorldIdsAsync();
                var validWorlds = new List<string>();

                foreach (var worldId in worldIds)
                {
                    if (await _storage.ExistsAsync(worldId))
                    {
                        validWorlds.Add(worldId);
                    }
                }

                AvailableWorlds.Clear();
                foreach (var worldId in validWorlds.AsValueEnumerable().OrderBy(x => x))
                {
                    AvailableWorlds.Add(worldId);
                }

                _logger.LogInfo($"[WorldRegistry] Найдено миров: {validWorlds.Count}");
            }
            finally
            {
                _isLoading.Value = false;
            }
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _currentWorldId?.Dispose();
            _isLoading?.Dispose();
            AvailableWorlds?.Clear();
        }
    }
}