using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;
using R3;
using Tayx.Graphy;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class SettingsUI : MonoBehaviour
    {
        [Header("Настройки игры")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle shadowToggle, joystickToggle, particlesToggle, profileToggle;
        [SerializeField] private GameObject profiler;
        
        private SettingsModel _settingsModel;
        private GraphyDebugger _profilerComponent;
        private CompositeDisposable _disposables = new();

        private void Start()
        {
            _settingsModel = ServiceLocator.Services.Get<SettingsModel>();
            _profilerComponent = profiler?.GetComponent<GraphyDebugger>();
            
            InitializeUI();
            BindToModel();
        }

        private void InitializeUI()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(QualitySettings.names.AsValueEnumerable().ToList());
            
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            shadowToggle.onValueChanged.AddListener(OnShadowsChanged);
            joystickToggle.onValueChanged.AddListener(OnJoystickChanged);
            particlesToggle.onValueChanged.AddListener(OnParticlesChanged);
            profileToggle.onValueChanged.AddListener(OnProfilingChanged);
        }

        private void BindToModel()
        {
            _settingsModel.GameSettings
                .Subscribe(UpdateGameUI)
                .AddTo(_disposables);
        }

        private void UpdateGameUI(GameSettings settings)
        {
            qualityDropdown.SetValueWithoutNotify(settings.qualityLevel);
            shadowToggle.SetIsOnWithoutNotify(settings.enableShadows);
            joystickToggle.SetIsOnWithoutNotify(settings.controlType == ControlType.Mobile);
            particlesToggle.SetIsOnWithoutNotify(settings.enableParticles);
            profileToggle.SetIsOnWithoutNotify(settings.enableProfiling);
            
            QualitySettings.SetQualityLevel(settings.qualityLevel);
            _profilerComponent?.gameObject.SetActive(settings.enableProfiling);
        }

        private void OnQualityChanged(int value)
        {
            var settings = _settingsModel.GameSettings.CurrentValue.Clone();
            settings.qualityLevel = value;
            _settingsModel.UpdateGameSettings(settings);
        }

        private void OnShadowsChanged(bool value)
        {
            var settings = _settingsModel.GameSettings.CurrentValue.Clone();
            settings.enableShadows = value;
            _settingsModel.UpdateGameSettings(settings);
        }

        private void OnJoystickChanged(bool value)
        {
            var settings = _settingsModel.GameSettings.CurrentValue.Clone();
            settings.controlType = value ? ControlType.Mobile : ControlType.Personal;
            _settingsModel.UpdateGameSettings(settings);
        }

        private void OnParticlesChanged(bool value)
        {
            var settings = _settingsModel.GameSettings.CurrentValue.Clone();
            settings.enableParticles = value;
            _settingsModel.UpdateGameSettings(settings);
        }

        private void OnProfilingChanged(bool value)
        {
            var settings = _settingsModel.GameSettings.CurrentValue.Clone();
            settings.enableProfiling = value;
            _settingsModel.UpdateGameSettings(settings);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}