using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using ZLinq;
using System;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldSettingsUI : MonoBehaviour
    {
        [Header("Конфигурация мира")]
        [SerializeField] private TMP_Dropdown autosaveDropdown;
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private TMP_InputField timeScaleInput;
        [SerializeField] private TMP_InputField maxEntityCountInput;
        [SerializeField] private TMP_InputField worldNameInput;
        [SerializeField] private Toggle generateStructures;
        [SerializeField] private Toggle generateRivers;
        [SerializeField] private Button saveWorldNameButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject loadingPanel;

        private WorldSettingsController _controller;
        private WorldRegistry _registry;
        private CompositeDisposable _disposables = new();

        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = 
            { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };

        private void Start()
        {
            _controller = ServiceLocator.GetService<WorldSettingsController>();
            _registry = ServiceLocator.GetService<WorldRegistry>();

            InitializeControls();
            BindToModel();
        }

        private void InitializeControls()
        {
            SetupAutosaveDropdown();
            SetupDifficultyDropdown();
            SetupInputFields();
            SetupToggles();

            panel?.SetActive(false);
        }

        private void SetupAutosaveDropdown()
        {
            autosaveDropdown.ClearOptions();
            autosaveDropdown.AddOptions(_autosaveLabels.AsValueEnumerable().ToList());
            autosaveDropdown.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < _autosaveIntervals.Length)
                {
                    _controller.Update(c => c.autosaveInterval = _autosaveIntervals[index]);
                }
            });
        }

        private void SetupDifficultyDropdown()
        {
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(Enum.GetNames(typeof(DifficultyLevel)).AsValueEnumerable().ToList());
            difficultyDropdown.onValueChanged.AddListener(value =>
                _controller.Update(c => c.difficulty = (DifficultyLevel)value));
        }

        private void SetupInputFields()
        {
            saveWorldNameButton.onClick.AddListener(() =>
            {
                var newName = worldNameInput.text.Trim();
                if (!string.IsNullOrEmpty(newName))
                {
                    _controller.RenameWorldAsync(newName).Forget();
                }
            });

            timeScaleInput.onEndEdit.AddListener(value =>
            {
                if (float.TryParse(value, out float scale))
                {
                    _controller.Update(c => c.timeScale = Mathf.Clamp(scale, 0.1f, 5.0f));
                }
            });

            maxEntityCountInput.onEndEdit.AddListener(value =>
            {
                if (int.TryParse(value, out int count))
                {
                    _controller.Update(c => c.maxEntityCount = Mathf.Max(100, count));
                }
            });
        }

        private void SetupToggles()
        {
            generateStructures.onValueChanged.AddListener(value =>
                _controller.Update(c => c.generateStructures = value));

            generateRivers.onValueChanged.AddListener(value =>
                _controller.Update(c => c.generateRivers = value));
        }

        private void BindToModel()
        {
            _controller.Config
                .Subscribe(UpdateView)
                .AddTo(_disposables);

            _registry.CurrentWorldId
                .Subscribe(worldId => panel?.SetActive(!string.IsNullOrEmpty(worldId)))
                .AddTo(_disposables);

            _registry.IsLoading
                .Subscribe(isLoading => loadingPanel?.SetActive(isLoading))
                .AddTo(_disposables);
        }

        private void UpdateView(WorldConfiguration config)
        {
            if (config == null) return;

            worldNameInput.SetTextWithoutNotify(config.worldName);

            int autosaveIndex = Array.IndexOf(_autosaveIntervals, config.autosaveInterval);
            autosaveDropdown.SetValueWithoutNotify(Mathf.Max(0, autosaveIndex));

            timeScaleInput.SetTextWithoutNotify(config.timeScale.ToString("F2"));
            maxEntityCountInput.SetTextWithoutNotify(config.maxEntityCount.ToString());
            difficultyDropdown.SetValueWithoutNotify((int)config.difficulty);
            generateStructures.SetIsOnWithoutNotify(config.generateStructures);
            generateRivers.SetIsOnWithoutNotify(config.generateRivers);
        }

        private void OnDestroy() => _disposables?.Dispose();
    }
}