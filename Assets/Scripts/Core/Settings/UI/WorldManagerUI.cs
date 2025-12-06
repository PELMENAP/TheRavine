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
        [SerializeField] private Button createWorldButton, confirmCreateWorldButton, cancelCreateWorldButton;
        [SerializeField] private Button backToWorldList;
        [SerializeField] private TMP_InputField newWorldNameInput;
        [SerializeField] private GameObject createWorldPanel, chooseWorldPanel;
        
        [Header("Интеграции")]
        [SerializeField] private WorldSettingsUI settingsView;
        
        private IRavineLogger _logger;
        private WorldRegistry _worldRegistry;
        private WorldStorage _worldStorage;
        private readonly CompositeDisposable disposables = new();

        private void Start()
        {
            _worldRegistry = ServiceLocator.GetService<WorldRegistry>();
            _logger = ServiceLocator.GetService<IRavineLogger>();
            _worldStorage = ServiceLocator.GetService<WorldStorage>();
            
            InitializeUI();
            BindToModel();
        }

        private void InitializeUI()
        {
            createWorldButton.onClick.AddListener(OnCreateWorldButtonClick);
            confirmCreateWorldButton.onClick.AddListener(OnConfirmCreateWorld);
            cancelCreateWorldButton.onClick.AddListener(OnCancelCreateWorld);
            backToWorldList.onClick.AddListener(OnEndEditWorld);
            createWorldPanel?.SetActive(false);
            chooseWorldPanel?.SetActive(true);
        }

        private void BindToModel()
        {
            _worldRegistry.AvailableWorlds.CollectionChanged += UpdateWorldsList;
            
            _worldRegistry.CurrentWorld
                .Subscribe(_ => RefreshWorldsListUI())
                .AddTo(disposables);
            
            UpdateWorldsList(default);
        }

        private void UpdateWorldsList(in NotifyCollectionChangedEventArgs<string> e)
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
                    _worldStorage,
                    _worldRegistry);
            }
        }

        private async void OnEnterWorld(string worldName)
        {
            bool success = await _worldRegistry.LoadWorldAsync(worldName);
            if (!success)
            {
                _logger.LogError($"Не удалось войти в мир: {worldName}");
            }
        }

        private async void OnDeleteWorld(string worldName)
        {
            if (await ShowConfirmationDialog($"Удалить мир '{worldName}'?"))
            {
                bool success = await _worldRegistry.DeleteWorldAsync(worldName);
                if (!success)
                {
                    _logger.LogError($"Не удалось удалить мир: {worldName}");
                }
            }
        }

        private void OnEditWorldSettings(string worldName)
        {
            if (settingsView != null)
            {
                settingsView.EditWorld(worldName);
            }
            else
            {
                _logger.LogWarning("SettingsView не назначен в WorldManagerView");
            }
        }

        private void OnCreateWorldButtonClick()
        {
            ShowCreateWorldPanel(true);
            newWorldNameInput.text = "";
        }

        public async void OnConfirmCreateWorld()
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
                ShowCreateWorldPanel(false);
                OnEditWorldSettings(worldName);
            }
        }

        public void OnEndEditWorld()
        {
            ShowCreateWorldPanel(false);
            settingsView.EditWorld(null);
        }

        public void OnCancelCreateWorld()
        {
            ShowCreateWorldPanel(false);
        }

        private void ShowCreateWorldPanel(bool show)
        {
            createWorldPanel?.SetActive(show);
            chooseWorldPanel?.SetActive(!show);
        }

        private async UniTask<bool> ShowConfirmationDialog(string message)
        {
            _logger.LogInfo($"Confirmation: {message}");
            await UniTask.Yield();
            return true;
        }

        private void OnDestroy()
        {
            disposables?.Dispose();
            
            if (_worldRegistry != null)
            {
                _worldRegistry.AvailableWorlds.CollectionChanged -= UpdateWorldsList;
            }
        }
    }
}