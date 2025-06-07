using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using TMPro;
using R3;
using Tayx.Graphy;
using ObservableCollections;
using Cysharp.Threading.Tasks;

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
        [SerializeField] private TMP_InputField timeScaleInput;
        [SerializeField] private Toggle pauseOnFocusToggle;
        [SerializeField] private TMP_InputField maxEntityCountInput;
        [SerializeField] private Toggle worldSettingsToggle;
        [SerializeField] private GameObject worldSettingsPanel;
        
        [Header("Управление мирами")]
        [SerializeField] private ScrollRect worldsScrollView;
        [SerializeField] private Transform worldsContainer;
        [SerializeField] private GameObject worldItemPrefab;
        [SerializeField] private Button createWorldButton;
        [SerializeField] private TMP_InputField newWorldNameInput;
        [SerializeField] private GameObject createWorldPanel;
        
        private ISettingsModel _settingsModel;
        private IWorldDataService _worldDataService;
        private IWorldManager _worldManager;
        private GraphyDebugger _profilerComponent;
        private CompositeDisposable _disposables = new();
        
        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };
        
        private string _currentEditingWorld = null;

        private void Start()
        {
            _settingsModel = ServiceLocator.GetSettings();
            _worldDataService = ServiceLocator.GetWorldDataService();
            _worldManager = ServiceLocator.GetWorldManager();
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
            
            timeScaleInput.onValueChanged.AddListener(OnTimeScaleChanged);
            pauseOnFocusToggle.onValueChanged.AddListener(OnPauseOnFocusChanged);
            maxEntityCountInput.onValueChanged.AddListener(OnMaxEntityCountChanged);

            worldSettingsToggle.onValueChanged.AddListener(OnWorldSettingsToggle);
            
            createWorldButton.onClick.AddListener(OnCreateWorldButtonClick);
            
            worldSettingsPanel?.SetActive(false);
            createWorldPanel?.SetActive(false);
        }

        private void BindToModel()
        {
            _settingsModel.GameSettings
                .Subscribe(UpdateGameUI)
                .AddTo(_disposables);

            _settingsModel.WorldSettings
                .Subscribe(UpdateWorldUI)
                .AddTo(_disposables);

            _worldManager.AvailableWorlds.CollectionChanged += UpdateWorldsList;

            _worldManager.CurrentWorld
                .Subscribe(worldName => 
                {
                    bool hasWorld = !string.IsNullOrEmpty(worldName);
                    worldSettingsToggle.gameObject.SetActive(hasWorld);
                    if (!hasWorld && worldSettingsPanel != null)
                    {
                        worldSettingsPanel.SetActive(false);
                        _currentEditingWorld = null;
                    }
                })
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
            timeScaleInput.SetTextWithoutNotify(settings.timeScale.ToString("F2"));
            pauseOnFocusToggle.SetIsOnWithoutNotify(settings.pauseOnFocusLoss);
            maxEntityCountInput.SetTextWithoutNotify(settings.maxEntityCount.ToString());
        }

        private void UpdateWorldsList(in NotifyCollectionChangedEventArgs<string> e)
        {
            foreach (Transform child in worldsContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var worldName in _worldManager.AvailableWorlds)
            {
                CreateWorldItem(worldName);
            }
        }

        private void CreateWorldItem(string worldName)
        {
            var worldItem = Instantiate(worldItemPrefab, worldsContainer);
            var worldItemComponent = worldItem.GetComponent<WorldItemUI>();
            
            if (worldItemComponent != null)
            {
                worldItemComponent.Initialize(worldName, 
                    () => OnEnterWorld(worldName),
                    () => OnDeleteWorld(worldName),
                    () => OnEditWorldSettings(worldName));
            }
        }

        private async void OnEnterWorld(string worldName)
        {
            bool success = await _worldManager.LoadWorldAsync(worldName);
            if (success)
            {
                Debug.Log($"Вошли в мир: {worldName}");
            }
            else
            {
                Debug.LogError($"Не удалось войти в мир: {worldName}");
            }
        }

        private async void OnDeleteWorld(string worldName)
        {
            // Можно добавить диалог подтверждения
            bool success = await _worldManager.DeleteWorldAsync(worldName);
            if (success)
            {
                Debug.Log($"Мир удален: {worldName}");
            }
            else
            {
                Debug.LogError($"Не удалось удалить мир: {worldName}");
            }
        }

        private void OnEditWorldSettings(string worldName)
        {
            _currentEditingWorld = worldName;
            
            var worldSettings = LoadWorldSettingsForWorld(worldName);
            _settingsModel.UpdateWorldSettings(worldSettings);
            
            worldSettingsPanel?.SetActive(true);
            worldSettingsToggle.SetIsOnWithoutNotify(true);
        }

        private WorldSettings LoadWorldSettingsForWorld(string worldName)
        {
            var settingsFile = $"{worldName}_world_settings";
            if (SaveLoad.FileExists(settingsFile))
                return SaveLoad.LoadEncryptedData<WorldSettings>(settingsFile);
            return new WorldSettings { worldName = worldName };
        }

        private void OnCreateWorldButtonClick()
        {
            createWorldPanel?.SetActive(true);
            newWorldNameInput.text = "";
        }

        public async void OnConfirmCreateWorld()
        {
            string worldName = newWorldNameInput.text.Trim();
            
            if (string.IsNullOrEmpty(worldName))
            {
                Debug.LogWarning("Имя мира не может быть пустым");
                return;
            }

            bool success = await _worldManager.CreateWorldAsync(worldName);
            if (success)
            {
                Debug.Log($"Мир создан: {worldName}");
                createWorldPanel?.SetActive(false);
                
                OnEditWorldSettings(worldName);
            }
            else
            {
                Debug.LogError($"Не удалось создать мир: {worldName}");
            }
        }

        public void OnCancelCreateWorld()
        {
            createWorldPanel?.SetActive(false);
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

        private void OnPauseOnFocusChanged(bool value)
        {
            var settings = GetCurrentWorldSettings();
            settings.pauseOnFocusLoss = value;
            SaveWorldSettings(settings);
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

        private void OnWorldSettingsToggle(bool isOn)
        {
            worldSettingsPanel?.SetActive(isOn);
            if (!isOn)
            {
                _currentEditingWorld = null;
            }
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}