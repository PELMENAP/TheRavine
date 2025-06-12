using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using System;
using ZLinq;

namespace TheRavine.Base
{
    public class WorldSettingsView : MonoBehaviour
    {
        [Header("Настройки мира")]
        [SerializeField] private TMP_Dropdown autosaveDropdown;
        [SerializeField] private TMP_InputField timeScaleInput;
        [SerializeField] private TMP_InputField maxEntityCountInput;
        [SerializeField] private GameObject worldSettingsPanel;
        [SerializeField] private TextMeshProUGUI currentWorldText;
        private ISettingsModel _settingsModel;
        private IWorldDataService _worldDataService;
        private IWorldManager _worldManager;
        private CompositeDisposable _disposables = new();
        
        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };
        
        private string _currentEditingWorld = null;
        
        public event Action<bool> OnWorldSettingsToggled;

        private void Start()
        {
            _settingsModel = ServiceLocator.GetSettings();
            _worldDataService = ServiceLocator.GetWorldDataService();
            _worldManager = ServiceLocator.GetWorldManager();
            
            InitializeUI();
            BindToModel();
        }

        private void InitializeUI()
        {
            autosaveDropdown.ClearOptions();
            autosaveDropdown.AddOptions(_autosaveLabels.AsValueEnumerable().ToList());
            autosaveDropdown.onValueChanged.AddListener(OnAutosaveIntervalChanged);
            
            timeScaleInput.onValueChanged.AddListener(OnTimeScaleChanged);
            maxEntityCountInput.onValueChanged.AddListener(OnMaxEntityCountChanged);
            
            worldSettingsPanel?.SetActive(false);
        }

        private void BindToModel()
        {
            _settingsModel.WorldSettings
                .Subscribe(UpdateWorldUI)
                .AddTo(_disposables);

            _worldManager.CurrentWorld
                .Subscribe(worldName => 
                {
                    bool hasWorld = !string.IsNullOrEmpty(worldName);
                    if (!hasWorld && worldSettingsPanel != null)
                    {
                        worldSettingsPanel.SetActive(false);
                        _currentEditingWorld = null;
                    }
                })
                .AddTo(_disposables);
        }

        private void UpdateWorldUI(WorldSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            int currentIndex = 0;
            for (int i = 0; i < _autosaveIntervals.Length; i++)
            {
                if (_autosaveIntervals[i] == settings.autosaveInterval)
                {
                    currentIndex = i;
                    break;
                }
            }
            
            currentWorldText.text = settings.worldName;
            autosaveDropdown.SetValueWithoutNotify(currentIndex);
            timeScaleInput.SetTextWithoutNotify(settings.timeScale.ToString("F2"));
            maxEntityCountInput.SetTextWithoutNotify(settings.maxEntityCount.ToString());
        }

        public void EditWorldSettings(string worldName)
        {
            _currentEditingWorld = worldName;
            
            var worldSettings = LoadWorldSettingsForWorld(worldName);
            _settingsModel.UpdateWorldSettings(worldSettings);
            
            worldSettingsPanel?.SetActive(true);
        }

        private WorldSettings LoadWorldSettingsForWorld(string worldName)
        {
            var settingsFile = $"{worldName}_world_settings";
            if (SaveLoad.FileExists(settingsFile))
                return SaveLoad.LoadEncryptedData<WorldSettings>(settingsFile);
            return new WorldSettings { worldName = worldName };
        }

        private void OnAutosaveIntervalChanged(int index)
        {
            if (index < 0 || index >= _autosaveIntervals.Length) return;

            var settings = GetCurrentWorldSettings();
            settings.autosaveInterval = _autosaveIntervals[index];
            SaveWorldSettings(settings);

            if (_worldDataService is WorldDataService worldDataService)
            {
                worldDataService.UpdateAutosaveInterval(settings.autosaveInterval);
            }
        }

        private void OnTimeScaleChanged(string value)
        {
            if (float.TryParse(value, out float timeScale))
            {
                var settings = GetCurrentWorldSettings();
                settings.timeScale = Mathf.Clamp(timeScale, 0.1f, 5.0f);
                SaveWorldSettings(settings);
            }
        }

        private void OnMaxEntityCountChanged(string value)
        {
            if (int.TryParse(value, out int count))
            {
                var settings = GetCurrentWorldSettings();
                settings.maxEntityCount = Mathf.Max(100, count);
                SaveWorldSettings(settings);
            }
        }

        private WorldSettings GetCurrentWorldSettings()
        {
            var current = _settingsModel.WorldSettings.CurrentValue;
            if (current != null && !string.IsNullOrEmpty(_currentEditingWorld))
            {
                current.worldName = _currentEditingWorld;
                return current.Clone();
            }
            return new WorldSettings { worldName = _currentEditingWorld ?? "Unknown" };
        }

        private void SaveWorldSettings(WorldSettings settings)
        {
            _settingsModel.UpdateWorldSettings(settings);
            
            if (!string.IsNullOrEmpty(_currentEditingWorld))
            {
                var settingsFile = $"{_currentEditingWorld}_world_settings";
                SaveLoad.SaveEncryptedData(settingsFile, settings);
            }
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}