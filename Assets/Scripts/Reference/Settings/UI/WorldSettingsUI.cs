using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using R3;
using ZLinq;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldSettingsUI : SettingsViewBase<WorldConfiguration>
    {
        [Header("Конфигурация мира")]
        [SerializeField] private TMP_Dropdown autosaveDropdown;
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private TMP_InputField timeScaleInput, maxEntityCountInput;
        [SerializeField] private Toggle debugModeToggle, cheatsToggle;
        [SerializeField] private TextMeshProUGUI worldNameText;
        [SerializeField] private GameObject panel;

        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = 
            { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };

        private string _editingWorldId;
        private WorldRegistry _registry;
        private WorldStatePersistence _persistence;

        protected override void Start()
        {
            base.Start();
            _registry = ServiceLocator.GetService<WorldRegistry>();
            _persistence = ServiceLocator.GetService<WorldStatePersistence>();

            _registry.CurrentWorld
                .Subscribe(OnActiveWorldChanged)
                .AddTo(Disposables);
        }

        protected override void InitializeControls()
        {
            SetupAutosaveDropdown();
            SetupInputFields();
            SetupDifficultyDropdown();
            SetupToggles();

            panel?.SetActive(false);
        }

        private void SetupAutosaveDropdown()
        {
            autosaveDropdown.ClearOptions();
            autosaveDropdown.AddOptions(_autosaveLabels.AsValueEnumerable().ToList());
            autosaveDropdown.onValueChanged.AddListener(OnAutosaveChanged);
        }

        private void SetupInputFields()
        {
            timeScaleInput.onValueChanged.AddListener(value =>
            {
                if (float.TryParse(value, out float scale))
                {
                    Mediator.UpdateWorldConfig(c => 
                        c.timeScale = Mathf.Clamp(scale, 0.1f, 5.0f));
                }
            });

            maxEntityCountInput.onValueChanged.AddListener(value =>
            {
                if (int.TryParse(value, out int count))
                {
                    Mediator.UpdateWorldConfig(c => 
                        c.maxEntityCount = Mathf.Max(100, count));
                }
            });
        }

        private void SetupDifficultyDropdown()
        {
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(
                Enum.GetNames(typeof(DifficultyLevel))
                    .AsValueEnumerable()
                    .ToList()
            );

            difficultyDropdown.onValueChanged.AddListener(value =>
                Mediator.UpdateWorldConfig(c => 
                    c.difficulty = (DifficultyLevel)value));
        }

        private void SetupToggles()
        {
            debugModeToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateWorldConfig(c => c.enableDebugMode = value));

            cheatsToggle.onValueChanged.AddListener(value =>
                Mediator.UpdateWorldConfig(c => c.enableCheats = value));
        }

        private void OnAutosaveChanged(int index)
        {
            if (index < 0 || index >= _autosaveIntervals.Length) return;

            var interval = _autosaveIntervals[index];

            Mediator.UpdateWorldConfig(c => c.autosaveInterval = interval);
            _persistence?.SetAutosaveInterval(interval);
        }

        protected override void BindToModel()
        {
            Mediator.WorldConfig
                .Subscribe(UpdateView)
                .AddTo(Disposables);
        }

        protected override void UpdateView(WorldConfiguration config)
        {
            if (config == null) return;

            Debug.Log(config.worldName); // имя меняется
            Debug.Log(config.autosaveInterval); // интервал и другие парамерты не меняются

            worldNameText.text = config.worldName;

            int autosaveIndex = Array.IndexOf(_autosaveIntervals, config.autosaveInterval);
            autosaveDropdown.SetValueWithoutNotify(Mathf.Max(0, autosaveIndex));

            timeScaleInput.SetTextWithoutNotify(config.timeScale.ToString("F2"));
            maxEntityCountInput.SetTextWithoutNotify(config.maxEntityCount.ToString());
            difficultyDropdown.SetValueWithoutNotify((int)config.difficulty);
            debugModeToggle.SetIsOnWithoutNotify(config.enableDebugMode);
            cheatsToggle.SetIsOnWithoutNotify(config.enableCheats);
        }

        private void OnActiveWorldChanged(string worldId)
        {
            var hasWorld = !string.IsNullOrEmpty(worldId);
            panel?.SetActive(hasWorld);

            if (!hasWorld)
                _editingWorldId = null;
        }

        public void EditWorld(string worldId)
        {
            _editingWorldId = worldId;
            Mediator.LoadWorldConfigAsync(worldId).Forget();
            panel?.SetActive(true);
        }
    }
}