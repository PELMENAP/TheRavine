using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

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

            _settings.Subscribe(ApplyDisplayAndGraphics).AddTo(_disposables);

            LoadAsync().Forget();
        }

        private static void ApplyDisplayAndGraphics(GlobalSettings s)
        {
            int quality = Mathf.Clamp(s.qualityLevel, 0, QualitySettings.count - 1);
            if (QualitySettings.GetQualityLevel() != quality)
                QualitySettings.SetQualityLevel(quality);

            QualitySettings.shadows = s.enableShadows ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.vSync || s.targetFps <= 0 ? -1 : s.targetFps;

            if (s.resolutionWidth > 0 && s.resolutionHeight > 0 &&
                (Screen.width != s.resolutionWidth || Screen.height != s.resolutionHeight))
            {
                Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, s.fullscreen);
            }

            FullScreenMode mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode)
                Screen.fullScreenMode = mode;
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

        public void Update(Action<GlobalSettings> modifier, bool persist = true)
        {
            if (modifier == null)
            {
                _logger.LogWarning("[GlobalSettings] Null модификатор");
                return;
            }

            var settings = _settings.Value.Clone();
            modifier(settings);
            _settings.Value = settings;

            if (persist) SaveAsync().Forget();
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