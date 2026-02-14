using System;
using Cysharp.Threading.Tasks;
using R3;

namespace TheRavine.Base
{
    public class GlobalSettingsController : IDisposable
    {
        private readonly GlobalSettingsRepository _repository;
        private readonly RavineLogger _logger;
        private readonly ReactiveProperty<GlobalSettings> _settings;
        private readonly CompositeDisposable _disposables = new();

        public ReadOnlyReactiveProperty<GlobalSettings> Settings { get; }

        public GlobalSettings GetCurrent() => _settings.Value;

        public GlobalSettingsController(IAsyncPersistentStorage persistenceStorage, RavineLogger logger)
        {
            _repository = new(persistenceStorage);
            _logger = logger;
            
            _settings = new ReactiveProperty<GlobalSettings>(new GlobalSettings());
            Settings = _settings.ToReadOnlyReactiveProperty();

            LoadAsync().Forget();
        }

        private async UniTask LoadAsync()
        {
            try
            {
                if (await _repository.ExistsAsync())
                {
                    var settings = await _repository.LoadAsync();
                    _settings.Value = settings;
                    _logger.LogInfo("[GlobalSettings] Настройки загружены");
                }
                else
                {
                    _logger.LogInfo("[GlobalSettings] Используются настройки по умолчанию");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GlobalSettings] Ошибка загрузки: {ex.Message}");
            }
        }

        public void Update(Action<GlobalSettings> modifier)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[GlobalSettings] Null модификатор");
                return;
            }

            var settings = _settings.Value.Clone();
            modifier(settings);
            _settings.Value = settings;

            SaveAsync().Forget();
        }

        public async UniTask<bool> SaveAsync()
        {
            try
            {
                await _repository.SaveAsync(_settings.Value);
                _logger.LogInfo("[GlobalSettings] Настройки сохранены");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[GlobalSettings] Ошибка сохранения: {ex.Message}");
                return false;
            }
        }

        public async UniTask ResetToDefaultAsync()
        {
            _settings.Value = new GlobalSettings();
            await SaveAsync();
            _logger.LogInfo("[GlobalSettings] Сброшены к умолчанию");
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _settings?.Dispose();
        }
    }
}