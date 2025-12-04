using System;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public class AutosaveSystem : IDisposable
    {
        private readonly Func<UniTask<bool>> _saveAction;
        private readonly IRavineLogger _logger;
        public readonly ReactiveProperty<bool> _isDirty;
        private readonly ReactiveProperty<int> _intervalSeconds;

        private IDisposable _timerSubscription;
        private readonly CompositeDisposable disposables = new();
        public Observable<int> IntervalSeconds { get; }

        public AutosaveSystem(
            Func<UniTask<bool>> saveAction,
            IRavineLogger logger,
            int initialInterval = 30)
        {
            _saveAction = saveAction;
            _logger = logger;

            _isDirty = new ReactiveProperty<bool>(false);
            _intervalSeconds = new ReactiveProperty<int>(initialInterval);
            IntervalSeconds = _intervalSeconds.AsObservable();

            _intervalSeconds
                .Subscribe(interval => RestartTimer(interval))
                .AddTo(disposables);
        }

        public void MarkDirty() => _isDirty.Value = true;
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
                .Where(_ => _isDirty.Value)
                .Subscribe(_ => TriggerSaveAsync().Forget())
                .AddTo(disposables);

            _logger.LogInfo($"Автосохранение установлено на {seconds} сек");
        }

        private async UniTaskVoid TriggerSaveAsync()
        {
            try
            {
                var success = await _saveAction();
                if (success)
                {
                    MarkClean();
                    _logger.LogInfo("Автосохранение выполнено");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка автосохранения: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timerSubscription?.Dispose();
            disposables?.Dispose();
            _isDirty?.Dispose();
            _intervalSeconds?.Dispose();
        }
    }
}