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

        private GraphyDebugger _profilerComponent;

        protected override void InitializeControls()
        {
            _profilerComponent = profiler?.GetComponent<GraphyDebugger>();

            SetupQualityDropdown();
            SetupToggles();
        }

        private void SetupQualityDropdown()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(
                QualitySettings.names
                    .AsValueEnumerable()
                    .ToList()
            );

            qualityDropdown.onValueChanged.AddListener(value =>
                Mediator.UpdateGlobal(s => s.qualityLevel = value));
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
            shadowToggle.SetIsOnWithoutNotify(settings.enableShadows);
            joystickToggle.SetIsOnWithoutNotify(settings.controlType == ControlType.Mobile);
            particlesToggle.SetIsOnWithoutNotify(settings.enableParticles);
            profileToggle.SetIsOnWithoutNotify(settings.enableProfiling);

            ApplySettings(settings);
        }

        private void ApplySettings(GlobalSettings settings)
        {
            QualitySettings.SetQualityLevel(settings.qualityLevel);
            _profilerComponent?.gameObject.SetActive(settings.enableProfiling);
        }
    }
}