using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;

using R3;

using Tayx.Graphy;

namespace TheRavine.Base
{
    public class SettingsView : MonoBehaviour
    {
        [Header("Настройки игры")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle shadowToggle, joystickToggle, particlesToggle, profileToggle;
        [SerializeField] private GameObject profiler;
        
        [Header("Настройки мира")]
        [SerializeField] private TMP_Dropdown autosaveDropdown;
        [SerializeField] private Toggle worldSettingsToggle;
        [SerializeField] private GameObject worldSettingsPanel;
        
        private ISettingsModel _settingsModel;
        private IWorldDataService _worldDataService;
        private GraphyDebugger _profilerComponent;
        private CompositeDisposable _disposables = new();
        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };

        private void Start()
        {
            _settingsModel = ServiceLocator.GetSettings();
            _worldDataService = ServiceLocator.GetWorldDataService();
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

            autosaveDropdown.ClearOptions();
            autosaveDropdown.AddOptions(_autosaveLabels.AsValueEnumerable().ToList());
            autosaveDropdown.onValueChanged.AddListener(OnAutosaveIntervalChanged);

            worldSettingsToggle.onValueChanged.AddListener(OnWorldSettingsToggle);
            
            worldSettingsPanel?.SetActive(false);
        }

        private void BindToModel()
        {
            _settingsModel.GameSettings
                .Subscribe(UpdateGameUI)
                .AddTo(_disposables);

            _settingsModel.WorldSettings
                .Subscribe(UpdateWorldUI)
                .AddTo(_disposables);

            var worldManager = ServiceLocator.GetWorldManager();
            if (worldManager != null)
            {
                worldManager.CurrentWorld
                    .Subscribe(worldName => 
                    {
                        bool hasWorld = !string.IsNullOrEmpty(worldName);
                        worldSettingsToggle.gameObject.SetActive(hasWorld);
                        if (!hasWorld && worldSettingsPanel != null)
                            worldSettingsPanel.SetActive(false);
                    })
                    .AddTo(_disposables);
            }
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

        private void UpdateWorldUI(WorldSettings settings)
        {
            if (settings == null) return;

            int currentIndex = 0;
            for (int i = 0; i < _autosaveIntervals.Length; i++)
            {
                if (_autosaveIntervals[i] == settings.autosaveInterval)
                {
                    currentIndex = i;
                    break;
                }
            }
            
            autosaveDropdown.SetValueWithoutNotify(currentIndex);
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

        private void OnAutosaveIntervalChanged(int index)
        {
            if (index < 0 || index >= _autosaveIntervals.Length) return;

            var settings = _settingsModel.WorldSettings.CurrentValue?.Clone() ?? new WorldSettings();
            settings.autosaveInterval = _autosaveIntervals[index];
            _settingsModel.UpdateWorldSettings(settings);

            if (_worldDataService is WorldDataService worldDataService)
            {
                worldDataService.UpdateAutosaveInterval(settings.autosaveInterval);
            }
        }

        private void OnWorldSettingsToggle(bool isOn)
        {
            worldSettingsPanel?.SetActive(isOn);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}