using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;
using R3;
using Tayx.Graphy;

namespace TheRavine.Base
{
    public class SettingsUI : SettingsViewBase<GlobalSettings>
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

        private GraphyDebugger _profilerComponent;

        private int[] grassDensityLevels = new int[] {1, 2, 3, 4, 5, 10, 20};

        protected override void InitializeControls()
        {
            _profilerComponent = profiler?.GetComponent<GraphyDebugger>();

            SetupDropdowns();
            SetupToggles();
        }

        private void SetupDropdowns()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(
                QualitySettings.names
                    .AsValueEnumerable()
                    .ToList()
            );
            qualityDropdown.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.qualityLevel = value));


            grassDensityDropdown.ClearOptions();
            grassDensityDropdown.AddOptions(
                grassDensityLevels
                    .AsValueEnumerable()
                    .Select(i => i.ToString())
                    .ToList()
            );
            grassDensityDropdown.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.grassDensityFactor = grassDensityLevels[value]));
        }

        private void SetupToggles()
        {
            shadowToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.enableShadows = value));

            joystickToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => 
                    s.controlType = value ? ControlType.Mobile : ControlType.Personal));

            particlesToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.enableParticles = value));

            profileToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.enableProfiling = value));

            grassShadows.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.enableGrassShadows = value));

            grassEnable.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.enableGrass = value));
        }

        protected override void BindToModel()
        {
            Mediator.Global
                .Subscribe(UpdateView)
                .AddTo(Disposables);
        }

        protected override void UpdateView(GlobalSettings settings)
        {
            qualityDropdown.SetValueWithoutNotify(settings.qualityLevel);
            grassDensityDropdown.SetValueWithoutNotify(grassDensityLevels.IndexOf(settings.grassDensityFactor));


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
    }
}