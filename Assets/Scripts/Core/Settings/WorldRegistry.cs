using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using R3;
using ObservableCollections;
using UnityEngine;
using ZLinq;

namespace TheRavine.Base
{
    public partial class WorldRegistry : IDisposable
    {
        public const int MaxWorldNameLength = 32;

        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

        private readonly WorldStorage storage;
        private readonly Action<PopulationSnapshot> checkPopulation;
        private readonly RavineLogger logger;
        
        private readonly ReactiveProperty<string> currentWorldId;
        private readonly ReactiveProperty<bool> isLoading;
        private readonly CompositeDisposable disposables = new();
        private readonly CancellationTokenSource lifetime = new();
        private bool disposed;

        public PopulationSnapshot LoadedPopulation { get; private set; }
        private WorldState currentState;
        private WorldConfiguration currentConfig;
        private bool hasUnsavedChanges;
        private long revision;
        internal object WorldSession => currentState;

        public ObservableList<string> AvailableWorlds { get; }
        public ReadOnlyReactiveProperty<string> CurrentWorldId { get; }
        public ReadOnlyReactiveProperty<bool> IsLoading { get; }

        public bool HasLoadedWorld => !string.IsNullOrEmpty(currentWorldId.Value);
        public string CurrentWorldName => currentConfig?.worldName ?? currentWorldId.Value;

        public string LastLoadError { get; private set; }

        public void ReportLoadError(string cause)
        {
            LastLoadError = cause;
            logger.LogError($"[WorldRegistry] World load failed: {cause}");
        }

        public WorldRegistry(
            IAsyncPersistentStorage persistenceStorage,
            RavineLogger logger, Action<PopulationSnapshot> checkPopulation = null)
        {
            WorldStateRepository worldStateRepo = new(persistenceStorage);
            WorldConfigRepository worldConfigRepo = new(persistenceStorage);
            storage = new WorldStorage(worldStateRepo, worldConfigRepo);
            this.logger = logger;
            this.checkPopulation = checkPopulation;
            
            currentWorldId = new ReactiveProperty<string>();
            isLoading = new ReactiveProperty<bool>(false);
            AvailableWorlds = new ObservableList<string>();

            CurrentWorldId = currentWorldId.ToReadOnlyReactiveProperty();
            IsLoading = isLoading.ToReadOnlyReactiveProperty();

            RefreshWorldListAsync().Forget();
        }

        public static bool IsValidWorldName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            string trimmed = name.Trim();
            if (trimmed.Length > MaxWorldNameLength) return false;
            if (trimmed == "." || trimmed == "..") return false;
            if (trimmed.IndexOfAny(InvalidNameChars) >= 0) return false;

            return trimmed.IndexOf('/') < 0 && trimmed.IndexOf('\\') < 0;
        }

        public async UniTask<bool> CreateWorldAsync(string worldName, WorldConfiguration customConfig = null)
        {
            if (!IsValidWorldName(worldName))
            {
                logger.LogWarning($"[WorldRegistry] Недопустимое имя мира: '{worldName}'");
                return false;
            }

            worldName = worldName.Trim();

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

            if ((hasUnsavedChanges || populationProvider != null || checkpointGate.CurrentCount == 0) && !await SaveCurrentWorldAsync()) return false;

            await checkpointGate.WaitAsync();
            try
            {
                if (disposed || !AvailableWorlds.Contains(worldId)) return false;
                isLoading.Value = true;
                var (state, config) = await storage.LoadFullAsync(worldId);
                if (disposed) return false;
                
                var population = PopulationRepository.Load(worldId, state.seed, config.createdTime);
                if (population != null) checkPopulation?.Invoke(population);
                LastLoadError = null;
                LoadedPopulation = population;
                populationProvider = null;
                currentState = state;
                currentConfig = config;
                hasUnsavedChanges = false;

                currentWorldId.Value = worldId;

                logger.LogInfo($"[WorldRegistry] Мир '{worldId}' загружен");
                return true;
            }
            catch (Exception ex)
            {
                ReportLoadError(ex.Message);
                return false;
            }
            finally
            {
                checkpointGate.Release();
                if (!disposed) isLoading.Value = false;
            }
        }

        public async UniTask<bool> UnloadWorldAsync()
        {
            if (!HasLoadedWorld) return true;

            if ((hasUnsavedChanges || populationProvider != null || checkpointGate.CurrentCount == 0) && !await SaveCurrentWorldAsync()) return false;

            var unloading = currentState;
            await checkpointGate.WaitAsync();
            try
            {
                if (disposed || !ReferenceEquals(currentState, unloading)) return false;
                populationProvider = null;
                LoadedPopulation = null;
                currentState = null;
                currentConfig = null;
                hasUnsavedChanges = false;
                currentWorldId.Value = null;
                logger.LogInfo("[WorldRegistry] Мир выгружен");
                return true;
            }
            finally
            {
                checkpointGate.Release();
            }
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
                    if (!await UnloadWorldAsync()) return false;
                }

                await storage.DeleteAsync(worldId);

                if (TryGetSafeWorldDirectory(worldId, out string directory))
                    DeleteDirectoryIfExists(directory);
                else
                    logger.LogWarning($"[WorldRegistry] Каталог мира '{worldId}' не удалён: имя недопустимо");
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

            if (!TryGetSafeWorldDirectory(oldName, out string oldDirectory))
            {
                logger.LogWarning($"[WorldRegistry] Недопустимое имя мира: '{oldName}'");
                return false;
            }

            if (!IsValidWorldName(newName))
            {
                logger.LogWarning($"[WorldRegistry] Недопустимое имя мира: '{newName}'");
                return false;
            }

            newName = newName.Trim();

            if (!TryGetSafeWorldDirectory(newName, out string newDirectory))
            {
                logger.LogWarning($"[WorldRegistry] Недопустимое имя мира: '{newName}'");
                return false;
            }

            if (AvailableWorlds.Contains(newName))
            {
                logger.LogWarning($"[WorldRegistry] Имя '{newName}' уже занято");
                return false;
            }

            if (currentWorldId.Value == oldName && !await SaveCurrentWorldAsync()) return false;

            await checkpointGate.WaitAsync();
            try
            {
                if (disposed || !AvailableWorlds.Contains(oldName) || AvailableWorlds.Contains(newName)) return false;
                isLoading.Value = true;
                bool isCurrent = currentWorldId.Value == oldName;
                WorldState state;
                WorldConfiguration config;
                if (isCurrent)
                {
                    state = currentState.Clone();
                    config = currentConfig.Clone();
                }
                else
                {
                    (state, config) = await storage.LoadFullAsync(oldName);
                }
                
                config.worldName = newName;
                config.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await storage.SaveFullAsync(newName, state, config);
                await storage.DeleteAsync(oldName);
                MoveDirectoryIfExists(oldDirectory, newDirectory);
                WorldRenamed?.Invoke(oldName, newName);

                var index = AvailableWorlds.IndexOf(oldName);
                AvailableWorlds.RemoveAt(index);
                AvailableWorlds.Insert(index, newName);

                if (isCurrent)
                {
                    currentConfig = config;
                    hasUnsavedChanges = false;
                    currentWorldId.Value = newName;
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
                checkpointGate.Release();
                if (!disposed) isLoading.Value = false;
            }
        }

        public event Action<string, string> WorldRenamed;

        public static string GetWorldDirectory(string worldId) => Path.Combine(Application.persistentDataPath, "worlds", worldId);

        public static bool TryGetSafeWorldDirectory(string worldId, out string path)
        {
            path = null;
            if (!IsValidWorldName(worldId) || worldId != worldId.Trim()) return false;

            string root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "worlds"));
            string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, worldId));

            if (full.Length <= prefix.Length || !full.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (full.IndexOf(Path.DirectorySeparatorChar, prefix.Length) >= 0 || full.IndexOf(Path.AltDirectorySeparatorChar, prefix.Length) >= 0) return false;

            path = full;
            return true;
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static void MoveDirectoryIfExists(string from, string to)
        {
            if (!Directory.Exists(from)) return;
            DeleteDirectoryIfExists(to);
            Directory.Move(from, to);
        }

        public string GetCurrentWorldId() => currentWorldId.Value;

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
            revision++;
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
            revision++;
            hasUnsavedChanges = true;
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
            if (disposed) return;
            var ct = lifetime.Token;

            try
            {
                isLoading.Value = true;
                ct.ThrowIfCancellationRequested();
                var worldIds = await storage.GetAllWorldIdsAsync(ct);
                ct.ThrowIfCancellationRequested();
                var validWorlds = new List<string>();

                foreach (var worldId in worldIds)
                {
                    if (!IsValidWorldName(worldId) || worldId != worldId.Trim())
                    {
                        logger.LogWarning($"[WorldRegistry] Запись мира с недопустимым именем пропущена: '{worldId}'");
                        continue;
                    }

                    bool exists = await storage.ExistsAsync(worldId, ct);
                    ct.ThrowIfCancellationRequested();
                    if (exists)
                    {
                        validWorlds.Add(worldId);
                    }
                }

                AvailableWorlds.Clear();
                foreach (var worldId in validWorlds.AsValueEnumerable().OrderBy(x => x))
                {
                    ct.ThrowIfCancellationRequested();
                    AvailableWorlds.Add(worldId);
                }

                if (!ct.IsCancellationRequested) logger.LogInfo($"[WorldRegistry] Найдено миров: {validWorlds.Count}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                if (!disposed) isLoading.Value = false;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            disposables.Dispose();
            currentWorldId?.Dispose();
            isLoading?.Dispose();
            AvailableWorlds?.Clear();
        }
    }
}