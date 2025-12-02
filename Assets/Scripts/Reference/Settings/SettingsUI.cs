using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;
using R3;
using Tayx.Graphy;

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
            
            qualityDropdown.onValueChanged.AddListener(value => 
                _settingsModel.ModifyGameSettings(s => s.qualityLevel = value));
            
            shadowToggle.onValueChanged.AddListener(value => 
                _settingsModel.ModifyGameSettings(s => s.enableShadows = value));
            
            joystickToggle.onValueChanged.AddListener(value => 
                _settingsModel.ModifyGameSettings(s => s.controlType = value ? ControlType.Mobile : ControlType.Personal));
            
            particlesToggle.onValueChanged.AddListener(value => 
                _settingsModel.ModifyGameSettings(s => s.enableParticles = value));
            
            profileToggle.onValueChanged.AddListener(value => 
                _settingsModel.ModifyGameSettings(s => s.enableProfiling = value));
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

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}