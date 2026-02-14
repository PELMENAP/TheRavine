using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using ObservableCollections;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldManagerUI : MonoBehaviour
    {
        [Header("Управление мирами")]
        [SerializeField] private ScrollRect worldsScrollView;
        [SerializeField] private Transform worldsContainer;
        [SerializeField] private GameObject worldItemPrefab;
        [SerializeField] private Button createWorldButton;
        [SerializeField] private Button confirmCreateWorldButton;
        [SerializeField] private Button cancelCreateWorldButton;
        [SerializeField] private Button backToWorldList;
        [SerializeField] private TMP_InputField newWorldNameInput;
        [SerializeField] private GameObject createWorldPanel;
        [SerializeField] private GameObject chooseWorldPanel;
        [SerializeField] private GameObject editWorldPanel;
        
        private RavineLogger _logger;
        private WorldRegistry _worldRegistry;
        private CompositeDisposable _disposables = new();

        private string _editingWorldId;

        private void Start()
        {
            _worldRegistry = ServiceLocator.GetService<WorldRegistry>();
            _logger = ServiceLocator.GetService<RavineLogger>();
            
            InitializeUI();
            BindToModel();
        }

        private void InitializeUI()
        {
            createWorldButton.onClick.AddListener(OnCreateWorldButtonClick);
            confirmCreateWorldButton.onClick.AddListener(OnConfirmCreateWorld);
            cancelCreateWorldButton.onClick.AddListener(OnCancelCreateWorld);
            backToWorldList.onClick.AddListener(OnBackToWorldList);
            
            ShowPanel(PanelType.WorldList);
        }

        private void BindToModel()
        {
            _worldRegistry.AvailableWorlds.CollectionChanged += OnWorldsListChanged;
            
            _worldRegistry.CurrentWorldId
                .Subscribe(_ => RefreshWorldsListUI())
                .AddTo(_disposables);
            
            RefreshWorldsListUI();
        }

        private void OnWorldsListChanged(in NotifyCollectionChangedEventArgs<string> e)
        {
            RefreshWorldsListUI();
        }

        private void RefreshWorldsListUI()
        {
            ClearWorldsList();
            
            foreach (var worldName in _worldRegistry.AvailableWorlds)
            {
                CreateWorldItem(worldName);
            }
        }

        private void ClearWorldsList()
        {
            foreach (Transform child in worldsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void CreateWorldItem(string worldName)
        {
            var worldItem = Instantiate(worldItemPrefab, worldsContainer);
            
            if (worldItem.TryGetComponent<WorldItemUI>(out var worldItemComponent))
            {
                worldItemComponent.Initialize(
                    worldName, 
                    () => OnEnterWorld(worldName),
                    () => OnDeleteWorld(worldName),
                    () => OnEditWorldSettings(worldName), 
                    _logger, 
                    _worldRegistry);
            }
        }

        private async void OnEnterWorld(string worldName)
        {
            bool success = await EnterWorldAndStartGameAsync(worldName);
            if (!success)
            {
                _logger.LogError($"Не удалось войти в мир: {worldName}");
            }
        }

        private async UniTask<bool> EnterWorldAndStartGameAsync(string worldName)
        {
            var success = await _worldRegistry.LoadWorldAsync(worldName);
            if (!success) return false;

            var sceneLaunchService = ServiceLocator.GetService<SceneLaunchService>();
            if (sceneLaunchService == null || !sceneLaunchService.CanLaunch)
            {
                _logger.LogError("SceneLaunchService недоступен");
                return false;
            }

            await sceneLaunchService.LaunchGame();
            
            var autosave = ServiceLocator.GetService<AutosaveCoordinator>();
            autosave?.Start();

            _logger.LogInfo($"Вход в мир '{worldName}' выполнен");
            return true;
        }

        private async void OnDeleteWorld(string worldName)
        {
            if (!await ShowConfirmationDialog($"Удалить мир '{worldName}'?"))
                return;

            bool success = await _worldRegistry.DeleteWorldAsync(worldName);
            if (!success)
            {
                _logger.LogError($"Не удалось удалить мир: {worldName}");
            }
        }

        private async void OnEditWorldSettings(string worldName)
        {
            var success = await _worldRegistry.LoadWorldAsync(worldName);
            if (success)
            {
                _editingWorldId = worldName;
                ShowPanel(PanelType.EditWorld);
            }
            else
            {
                _logger.LogError($"Не удалось загрузить мир для редактирования: {worldName}");
            }
        }

        private void OnCreateWorldButtonClick()
        {
            ShowPanel(PanelType.CreateWorld);
            newWorldNameInput.text = "";
        }

        private async void OnConfirmCreateWorld()
        {
            string worldName = newWorldNameInput.text.Trim();
            
            if (string.IsNullOrEmpty(worldName))
            {
                _logger.LogWarning("Имя мира не может быть пустым");
                return;
            }

            if (_worldRegistry.AvailableWorlds.Contains(worldName))
            {
                _logger.LogWarning($"Мир с именем '{worldName}' уже существует");
                return;
            }

            bool success = await _worldRegistry.CreateWorldAsync(worldName);
            if (success)
            {
                await _worldRegistry.LoadWorldAsync(worldName);
                _editingWorldId = worldName;
                ShowPanel(PanelType.EditWorld);
            }
        }

        private async void OnBackToWorldList()
        {
            if (!string.IsNullOrEmpty(_editingWorldId))
            {
                await _worldRegistry.SaveCurrentWorldAsync();
                await _worldRegistry.UnloadWorldAsync();
                _editingWorldId = null;
            }

            ShowPanel(PanelType.WorldList);
        }

        private void OnCancelCreateWorld()
        {
            ShowPanel(PanelType.WorldList);
        }

        private void ShowPanel(PanelType panelType)
        {
            createWorldPanel?.SetActive(panelType == PanelType.CreateWorld);
            chooseWorldPanel?.SetActive(panelType == PanelType.WorldList);
            editWorldPanel?.SetActive(panelType == PanelType.EditWorld);
        }

        private async UniTask<bool> ShowConfirmationDialog(string message)
        {
            _logger.LogInfo($"Confirmation: {message}");
            await UniTask.Yield();
            return true;
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
            
            if (_worldRegistry != null)
            {
                _worldRegistry.AvailableWorlds.CollectionChanged -= OnWorldsListChanged;
            }
        }

        private enum PanelType
        {
            WorldList,
            CreateWorld,
            EditWorld
        }
    }
}