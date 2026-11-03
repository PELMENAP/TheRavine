using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;
using R3;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private Button resetButton;

        [Header("Дисплей")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown fpsDropdown;

        [Header("Звук")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private float saveDebounceSeconds = 0.5f;

        private GlobalSettingsController _controller;
        private GraphyDebugger _profilerComponent;
        private CompositeDisposable _disposables = new();
        private CancellationTokenSource _saveCts;
        private bool _hasPendingSave;

        private readonly int[] _grassDensityLevels = { 1, 2, 3, 4, 5, 10, 20 };
        private readonly int[] _fpsLevels = { 0, 30, 60, 120 };
        private readonly List<Vector2Int> _resolutions = new();

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
            SetupDisplayControls();
            SetupSliders();

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
        }

        private void OnResetClicked() => _controller.ResetToDefaultAsync().Forget();

        private void SetupDisplayControls()
        {
            if (resolutionDropdown != null)
            {
                var labels = new List<string>();
                foreach (var resolution in Screen.resolutions)
                {
                    var size = new Vector2Int(resolution.width, resolution.height);
                    if (_resolutions.Contains(size)) continue;
                    _resolutions.Add(size);
                    labels.Add($"{size.x}x{size.y}");
                }

                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(labels);
                resolutionDropdown.onValueChanged.AddListener(value =>
                {
                    if (value < 0 || value >= _resolutions.Count) return;
                    var size = _resolutions[value];
                    _controller.Update(s =>
                    {
                        s.resolutionWidth = size.x;
                        s.resolutionHeight = size.y;
                    });
                });
            }

            if (fpsDropdown != null)
            {
                fpsDropdown.ClearOptions();
                fpsDropdown.AddOptions(new List<string> { "Без лимита", "30", "60", "120" });
                fpsDropdown.onValueChanged.AddListener(value =>
                {
                    if (value >= 0 && value < _fpsLevels.Length)
                        _controller.Update(s => s.targetFps = _fpsLevels[value]);
                });
            }

            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(value =>
                    _controller.Update(s => s.fullscreen = value));

            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(value =>
                    _controller.Update(s => s.vSync = value));
        }

        private void SetupSliders()
        {
            BindSlider(masterVolumeSlider, (s, v) => s.masterVolume = v);
            BindSlider(musicVolumeSlider, (s, v) => s.musicVolume = v);
            BindSlider(sfxVolumeSlider, (s, v) => s.sfxVolume = v);
        }

        private void BindSlider(Slider slider, Action<GlobalSettings, float> setter)
        {
            if (slider == null) return;

            slider.onValueChanged.AddListener(value =>
            {
                _controller.Update(s => setter(s, value), persist: false);
                ScheduleSave();
            });
        }

        private void ScheduleSave()
        {
            _hasPendingSave = true;
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            SaveAfterDelayAsync(_saveCts.Token).Forget();
        }

        private async UniTaskVoid SaveAfterDelayAsync(CancellationToken ct)
        {
            bool canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(saveDebounceSeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;

            FlushPendingSave();
        }

        private void FlushPendingSave()
        {
            if (!_hasPendingSave) return;
            _hasPendingSave = false;
            _controller.SaveAsync().Forget();
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

            if (resolutionDropdown != null && _resolutions.Count > 0)
                resolutionDropdown.SetValueWithoutNotify(FindResolutionIndex(settings.resolutionWidth, settings.resolutionHeight));
            if (fpsDropdown != null)
                fpsDropdown.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(_fpsLevels, settings.targetFps)));
            fullscreenToggle?.SetIsOnWithoutNotify(settings.fullscreen);
            vSyncToggle?.SetIsOnWithoutNotify(settings.vSync);
            masterVolumeSlider?.SetValueWithoutNotify(settings.masterVolume);
            musicVolumeSlider?.SetValueWithoutNotify(settings.musicVolume);
            sfxVolumeSlider?.SetValueWithoutNotify(settings.sfxVolume);

            ApplySettings(settings);
        }

        private int FindResolutionIndex(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                width = Screen.width;
                height = Screen.height;
            }

            for (int i = 0; i < _resolutions.Count; i++)
            {
                if (_resolutions[i].x == width && _resolutions[i].y == height) return i;
            }

            return _resolutions.Count - 1;
        }

        private void ApplySettings(GlobalSettings settings)
        {
            _profilerComponent?.gameObject.SetActive(settings.enableProfiling);
        }

        private void OnDisable() => FlushPendingSave();

        private void OnDestroy()
        {
            FlushPendingSave();
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _disposables?.Dispose();

            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnResetClicked);
        }
    }
}