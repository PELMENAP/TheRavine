using UnityEngine;
using TMPro;
using R3;
using ZLinq;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldSettingsUI : MonoBehaviour
    {
        [Header("Настройки мира")]
        [SerializeField] private TMP_Dropdown autosaveDropdown;
        [SerializeField] private TMP_InputField timeScaleInput;
        [SerializeField] private TMP_InputField maxEntityCountInput;
        [SerializeField] private GameObject worldSettingsPanel;
        [SerializeField] private TextMeshProUGUI currentWorldText;
        private SettingsModel _settingsModel;
        private WorldDataService _worldDataService;
        private WorldManager _worldManager;
        private WorldFileService _worldService;
        private readonly CompositeDisposable _disposables = new();
        
        private readonly int[] _autosaveIntervals = { 0, 15, 30, 60, 120, 300 };
        private readonly string[] _autosaveLabels = { "Отключено", "15 сек", "30 сек", "1 мин", "2 мин", "5 мин" };
        
        private string _currentEditingWorld = null;

        private void Start()
        {
            _settingsModel = ServiceLocator.GetService<SettingsModel>();
            _worldDataService = ServiceLocator.GetService<WorldDataService>();
            _worldManager = ServiceLocator.GetService<WorldManager>();
            _worldService = ServiceLocator.GetService<WorldFileService>();
            
            InitializeUI();
            BindToModel();
        }

        private void InitializeUI()
        {
            autosaveDropdown.ClearOptions();
            autosaveDropdown.AddOptions(_autosaveLabels.AsValueEnumerable().ToList());
            autosaveDropdown.onValueChanged.AddListener(OnAutosaveIntervalChangedWrapper);
            
            timeScaleInput.onValueChanged.AddListener(OnTimeScaleChangedWrapper);
            maxEntityCountInput.onValueChanged.AddListener(OnMaxEntityCountChangedWrapper);
            
            worldSettingsPanel?.SetActive(false);
        }

        private void OnAutosaveIntervalChangedWrapper(int index)
        {
            OnAutosaveIntervalChanged(index).Forget();
        }

        private void OnTimeScaleChangedWrapper(string value)
        {
            OnTimeScaleChanged(value).Forget();
        }

        private void OnMaxEntityCountChangedWrapper(string value)
        {
            OnMaxEntityCountChanged(value).Forget();
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

        public async UniTask EditWorldSettings(string worldName)
        {
            _currentEditingWorld = worldName;
            
            WorldSettings worldSettings = await LoadWorldSettingsForWorld(worldName);
            _settingsModel.ModifyWorldSettings(s => s = worldSettings);
            
            worldSettingsPanel?.SetActive(true);
        }

        private async UniTask<WorldSettings> LoadWorldSettingsForWorld(string worldName)
        {
            var settingsFile = $"{worldName}_world_settings";
            if (await _worldService.ExistsAsync(settingsFile))
                return await _worldService.LoadSettingsAsync(settingsFile);
            return new WorldSettings { worldName = worldName };
        }

        private async UniTask OnAutosaveIntervalChanged(int index)
        {
            if (index < 0 || index >= _autosaveIntervals.Length) return;

            var settings = GetCurrentWorldSettings();
            settings.autosaveInterval = _autosaveIntervals[index];
            await SaveWorldSettings(settings);

            if (_worldDataService is WorldDataService worldDataService)
            {
                worldDataService.UpdateAutosaveInterval(settings.autosaveInterval);
            }
        }

        private async UniTask OnTimeScaleChanged(string value)
        {
            if (float.TryParse(value, out float timeScale))
            {
                var settings = GetCurrentWorldSettings();
                settings.timeScale = Mathf.Clamp(timeScale, 0.1f, 5.0f);
                await SaveWorldSettings(settings);
            }
        }

        private async UniTask OnMaxEntityCountChanged(string value)
        {
            if (int.TryParse(value, out int count))
            {
                var settings = GetCurrentWorldSettings();
                settings.maxEntityCount = Mathf.Max(100, count);
                await SaveWorldSettings(settings);
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

        private async UniTask SaveWorldSettings(WorldSettings settings)
        {
            _settingsModel.ModifyWorldSettings(s => s = settings);
            
            if (!string.IsNullOrEmpty(_currentEditingWorld))
            {
                var settingsFile = $"{_currentEditingWorld}_world_settings";
                await _worldService.SaveSettingsAsync(settingsFile, settings);
            }
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }
    }
}