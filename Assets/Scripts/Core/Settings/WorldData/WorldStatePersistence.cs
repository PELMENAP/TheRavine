using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using R3;

namespace TheRavine.Base
{
    public class WorldStatePersistence : IDisposable
    {
        private readonly WorldStateRepository _repository;
        private readonly WorldRegistry _registry;
        private readonly AutosaveSystem _autosave;
        private readonly IRavineLogger _logger;

        private readonly ReactiveProperty<WorldState> _state;
        private readonly CompositeDisposable _disposables = new();
        
        private string _loadedWorldId;
        private bool _isAutosaveRegistered;
        private CancellationTokenSource _loadCts;

        public ReadOnlyReactiveProperty<WorldState> State { get; }

        public WorldStatePersistence(
            WorldStateRepository repository,
            WorldRegistry registry,
            AutosaveSystem autosave,
            IRavineLogger logger)
        {
            _repository = repository;
            _registry = registry;
            _autosave = autosave;
            _logger = logger;

            _state = new ReactiveProperty<WorldState>(new WorldState());
            State = _state.ToReadOnlyReactiveProperty();

            SubscribeToWorldChanges();
        }

        private void SubscribeToWorldChanges()
        {
            _registry.CurrentWorld
                .Subscribe(async worldId =>
                {
                    _loadCts?.Cancel();
                    _loadCts?.Dispose();
                    
                    if (string.IsNullOrEmpty(worldId))
                    {
                        UnloadWorld();
                    }
                    else
                    {
                        _loadCts = new CancellationTokenSource();
                        await LoadWorldAsync(worldId, _loadCts.Token);
                    }
                })
                .AddTo(_disposables);
        }
        private async UniTask LoadWorldAsync(string worldId, CancellationToken ct)
        {
            try
            {
                WorldState state;

                if (await _repository.ExistsAsync(worldId))
                {
                    state = await _repository.LoadAsync(worldId);
                    _logger.LogInfo($"[WorldState] Загружено состояние мира '{worldId}'");
                }
                else
                {
                    state = CreateNewWorldState();
                    _logger.LogInfo($"[WorldState] Создано новое состояние для '{worldId}'");
                }

                if (ct.IsCancellationRequested) return;

                _loadedWorldId = worldId;
                _state.Value = state;
                
                EnableAutosave();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"[WorldState] Загрузка '{worldId}' отменена");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldState] Ошибка загрузки '{worldId}': {ex.Message}");
            }
        }

        private void UnloadWorld()
        {
            _loadedWorldId = null;
            _state.Value = new WorldState();
            DisableAutosave();
            _logger.LogInfo("[WorldState] Мир выгружен");
        }

        private void EnableAutosave()
        {
            if (_isAutosaveRegistered) return;
            
            _autosave.AddSaveAction(SaveCurrentWorldAsync);
            _isAutosaveRegistered = true;
            _logger.LogInfo("[WorldState] Автосохранение активировано");
        }

        private void DisableAutosave()
        {
            if (!_isAutosaveRegistered) return;
            
            _autosave.RemoveSaveAction(SaveCurrentWorldAsync);
            _isAutosaveRegistered = false;
            _logger.LogInfo("[WorldState] Автосохранение отключено");
        }

        private WorldState CreateNewWorldState()
        {
            return new WorldState
            {
                seed = UnityEngine.Random.Range(0, int.MaxValue),
                lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds()
            };
        }

        public void UpdateState(Action<WorldState> modifier)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[WorldState] Попытка обновления с null модификатором");
                return;
            }

            if (string.IsNullOrEmpty(_loadedWorldId))
            {
                _logger.LogWarning("[WorldState] Попытка обновления без загруженного мира");
                return;
            }

            var state = _state.Value;
            modifier(state);
            _state.Value = state;
            _state.ForceNotify();
        }

        public void SetAutosaveInterval(int seconds)
        {
            _autosave.SetInterval(seconds);
        }

        private async UniTask<bool> SaveCurrentWorldAsync()
        {
            if (string.IsNullOrEmpty(_loadedWorldId))
            {
                _logger.LogWarning("[WorldState] Попытка сохранения без загруженного мира");
                return false;
            }

            return await SaveAsync(_loadedWorldId);
        }

        public async UniTask<bool> SaveAsync(string worldId = null, CancellationToken ct = default)
        {
            var targetWorldId = worldId ?? _loadedWorldId;
            
            if (string.IsNullOrEmpty(targetWorldId))
            {
                _logger.LogWarning("[WorldState] Попытка сохранения без worldId");
                return false;
            }

            try
            {
                var state = _state.Value;
                state.lastSaveTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                await _repository.SaveAsync(targetWorldId, state);
                
                _logger.LogInfo($"[WorldState] Состояние '{targetWorldId}' сохранено");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInfo($"[WorldState] Сохранение '{targetWorldId}' отменено");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[WorldState] Ошибка сохранения '{targetWorldId}': {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            
            DisableAutosave();
            
            _disposables?.Dispose();
            _state?.Dispose();
        }
    }
}