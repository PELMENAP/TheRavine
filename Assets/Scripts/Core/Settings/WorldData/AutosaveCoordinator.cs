using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class AutosaveCoordinator : IDisposable
    {
        private readonly WorldRegistry _worldRegistry;
        private readonly RavineLogger _logger;
        
        private int _intervalSeconds = 20;
        private bool _isRunning;
        private CancellationTokenSource _cts;

        public bool IsEnabled => _isRunning;
        public int IntervalSeconds => _intervalSeconds;

        public AutosaveCoordinator(WorldRegistry worldRegistry, RavineLogger logger)
        {
            _worldRegistry = worldRegistry;
            _logger = logger;
        }

        public void SetInterval(int seconds)
        {
            if (seconds < 0)
            {
                _logger.LogWarning($"[Autosave] Некорректный интервал: {seconds}");
                return;
            }

            var wasRunning = _isRunning;
            if (wasRunning) Stop();

            _intervalSeconds = seconds;
            _logger.LogInfo($"[Autosave] Интервал установлен: {seconds}с");

            if (wasRunning && seconds > 0) Start();
        }

        public void Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("[Autosave] Уже запущено");
                return;
            }

            if (_intervalSeconds <= 0)
            {
                _logger.LogInfo("[Autosave] Автосохранение отключено (интервал 0)");
                return;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            RunAutosaveLoopAsync(_cts.Token).Forget();
            _logger.LogInfo($"[Autosave] Запущено (интервал {_intervalSeconds}с)");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _isRunning = false;

            _logger.LogInfo("[Autosave] Остановлено");
        }

        private async UniTaskVoid RunAutosaveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(_intervalSeconds * 1000, cancellationToken: ct);
                    
                    if (ct.IsCancellationRequested) break;

                    if (_worldRegistry.HasLoadedWorld)
                    {
                        var success = await _worldRegistry.SaveCurrentWorldAsync();
                        if (success)
                        {
                            _logger.LogInfo("[Autosave] Мир сохранён");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Autosave] Ошибка: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}