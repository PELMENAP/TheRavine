using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using R3;

namespace TheRavine.Base
{
    public class AutosaveSystem : IDisposable
    {
        private readonly List<Func<UniTask<bool>>> _saveActions;
        private readonly IRavineLogger _logger;
        public readonly ReactiveProperty<bool> _isDirty;
        private readonly ReactiveProperty<int> _intervalSeconds;
        private IDisposable _timerSubscription;
        private readonly CompositeDisposable _disposables = new();
        
        public Observable<int> IntervalSeconds { get; }
        
        public AutosaveSystem(IRavineLogger logger, int initialInterval = 10)
        {
            _saveActions = new List<Func<UniTask<bool>>>();
            _logger = logger;
            _isDirty = new ReactiveProperty<bool>(false);
            _intervalSeconds = new ReactiveProperty<int>(initialInterval);
            IntervalSeconds = _intervalSeconds.AsObservable();
            
            _intervalSeconds
                .Subscribe(interval => RestartTimer(interval))
                .AddTo(_disposables);
        }
        
        public void AddSaveAction(Func<UniTask<bool>> saveAction)
        {
            if (saveAction == null)
            {
                _logger.LogWarning("Попытка добавить null действие сохранения");
                return;
            }

            _logger.LogWarning("Автодействие сохранено");
            
            _saveActions.Add(saveAction);
        }
        
        public bool RemoveSaveAction(Func<UniTask<bool>> saveAction)
        {
            return _saveActions.Remove(saveAction);
        }
        
        public void ClearSaveActions()
        {
            _saveActions.Clear();
        }
        
        public void MarkDirty()
        {
            _logger.LogWarning("MarkDirty вызван");
            _isDirty.Value = true;
        }
        public void MarkClean() => _isDirty.Value = false;
        
        public void SetInterval(int seconds)
        {
            if (seconds < 0)
            {
                _logger.LogWarning("Интервал автосохранения не может быть отрицательным");
                return;
            }
            _intervalSeconds.Value = seconds;
        }
        
        private void RestartTimer(int seconds)
        {
            _timerSubscription?.Dispose();
            
            if (seconds <= 0)
            {
                _logger.LogInfo("Автосохранение отключено");
                return;
            }
            
            _timerSubscription = Observable
                .Interval(TimeSpan.FromSeconds(seconds))
                .WithLatestFrom(_isDirty, (_, dirty) => dirty)
                .Where(dirty => dirty)
                .Subscribe(_ => TriggerSaveAsync());
            
            _logger.LogInfo($"Автосохранение установлено на {seconds} сек");
        }
        
        private void TriggerSaveAsync()
        {
            _logger.LogInfo("триггер автосохранения");

            if (_saveActions.Count == 0)
            {
                _logger.LogWarning("Нет зарегистрированных действий сохранения");
                return;
            }
            
            var allSucceeded = true;
            
            for (int i = 0; i < _saveActions.Count; i++)
            {
                _logger.LogInfo("one save action");
                try
                {
                    _saveActions[i]().Forget();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Ошибка автосохранения действия {i}: {ex.Message}");
                    allSucceeded = false;
                }
            }
            
            if (allSucceeded)
            {
                MarkClean();
                _logger.LogInfo($"Автосохранение выполнено ({_saveActions.Count} действий)");
            }
        }
        
        public void Dispose()
        {
            _timerSubscription?.Dispose();
            _disposables?.Dispose();
            _isDirty?.Dispose();
            _intervalSeconds?.Dispose();
        }
    }
}