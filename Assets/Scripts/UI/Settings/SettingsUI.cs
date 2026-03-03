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
        [Header("Игровые настройки")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle shadowToggle;
        [SerializeField] private Toggle joystickToggle;
        [SerializeField] private Toggle particlesToggle;
        [SerializeField] private Toggle profileToggle;
        [SerializeField] private GameObject profiler;
        [SerializeField] private Toggle grassShadows;
        [SerializeField] private Toggle grassEnable;
        [SerializeField] private TMP_Dropdown grassDensityDropdown;

        private GlobalSettingsController _controller;
        private GraphyDebugger _profilerComponent;
        private CompositeDisposable _disposables = new();

        private readonly int[] _grassDensityLevels = { 1, 2, 3, 4, 5, 10, 20 };

        private void Start()
        {
            _controller = ServiceLocator.GetService<GlobalSettingsController>();
            _profilerComponent = profiler?.GetComponent<GraphyDebugger>();

            InitializeControls();
            BindToModel();
        }

        private void InitializeControls()
        {
            SetupQualityDropdown();
            SetupGrassDensityDropdown();
            SetupToggles();
        }

        private void SetupQualityDropdown()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(QualitySettings.names.AsValueEnumerable().ToList());
            qualityDropdown.onValueChanged.AddListener(value =>
                _controller.Update(s => s.qualityLevel = value));
        }

        private void SetupGrassDensityDropdown()
        {
            grassDensityDropdown.ClearOptions();
            grassDensityDropdown.AddOptions(
                _grassDensityLevels.AsValueEnumerable().Select(i => i.ToString()).ToList());
            grassDensityDropdown.onValueChanged.AddListener(value =>
                _controller.Update(s => s.grassDensityFactor = _grassDensityLevels[value]));
        }

        private void SetupToggles()
        {
            shadowToggle.onValueChanged.AddListener(value =>
                _controller.Update(s => s.enableShadows = value));

            joystickToggle.onValueChanged.AddListener(value =>
                _controller.Update(s => s.controlType = value ? ControlType.Mobile : ControlType.Personal));

            particlesToggle.onValueChanged.AddListener(value =>
                _controller.Update(s => s.enableParticles = value));

            profileToggle.onValueChanged.AddListener(value =>
                _controller.Update(s => s.enableProfiling = value));

            grassShadows.onValueChanged.AddListener(value =>
                _controller.Update(s => s.enableGrassShadows = value));

            grassEnable.onValueChanged.AddListener(value =>
                _controller.Update(s => s.enableGrass = value));
        }

        private void BindToModel()
        {
            _controller.Settings
                .Subscribe(UpdateView)
                .AddTo(_disposables);
        }

        private void UpdateView(GlobalSettings settings)
        {
            qualityDropdown.SetValueWithoutNotify(settings.qualityLevel);
            grassDensityDropdown.SetValueWithoutNotify(_grassDensityLevels.IndexOf(settings.grassDensityFactor));
            shadowToggle.SetIsOnWithoutNotify(settings.enableShadows);
            joystickToggle.SetIsOnWithoutNotify(settings.controlType == ControlType.Mobile);
            particlesToggle.SetIsOnWithoutNotify(settings.enableParticles);
            profileToggle.SetIsOnWithoutNotify(settings.enableProfiling);
            grassShadows.SetIsOnWithoutNotify(settings.enableGrassShadows);
            grassEnable.SetIsOnWithoutNotify(settings.enableGrass);

            ApplySettings(settings);
        }

        private void ApplySettings(GlobalSettings settings)
        {
            QualitySettings.SetQualityLevel(settings.qualityLevel);
            _profilerComponent?.gameObject.SetActive(settings.enableProfiling);
        }

        private void OnDestroy() => _disposables?.Dispose();
    }
}