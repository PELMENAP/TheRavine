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
        
        private IRavineLogger logger;
        private WorldRegistry worldRegistry;
        private WorldStorage worldStorage;
        private readonly CompositeDisposable disposables = new();

        private void Start()
        {
            worldRegistry = ServiceLocator.GetService<WorldRegistry>();
            logger = ServiceLocator.GetService<IRavineLogger>();
            worldStorage = ServiceLocator.GetService<WorldStorage>();
            
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
            worldRegistry.AvailableWorlds.CollectionChanged += UpdateWorldsList;
            
            UpdateWorldsList(default);
        }

        private void UpdateWorldsList(in NotifyCollectionChangedEventArgs<string> e)
        {
            ClearWorldsList();
            
            foreach (var worldName in worldRegistry.AvailableWorlds)
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
            var worldItemComponent = worldItem.GetComponent<WorldItemUI>();
            
            if (worldItemComponent != null)
            {
                worldItemComponent.Initialize(worldName, 
                    () => OnEnterWorld(worldName),
                    () => OnDeleteWorld(worldName),
                    () =>  OnEditWorldSettings(worldName), logger, worldStorage);
            }
        }
        private async void OnEnterWorld(string worldName)
        {
            bool success = await worldRegistry.LoadWorldAsync(worldName);
            if (success)
            {
                logger.LogInfo($"Вошли в мир: {worldName}");
            }
            else
            {
                logger.LogError($"Не удалось войти в мир: {worldName}");
            }
        }

        private async void OnDeleteWorld(string worldName)
        {
            if (await ShowConfirmationDialog($"Удалить мир '{worldName}'?"))
            {
                bool success = await worldRegistry.DeleteWorldAsync(worldName);
                if (success)
                {
                    logger.LogInfo($"Мир удален: {worldName}");
                }
                else
                {
                    logger.LogError($"Не удалось удалить мир: {worldName}");
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
                logger.LogWarning("SettingsView не назначен в WorldManagerView");
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
                logger.LogWarning("Имя мира не может быть пустым");
                return;
            }

            if (worldRegistry.AvailableWorlds.Contains(worldName))
            {
                logger.LogWarning($"Мир с именем '{worldName}' уже существует");
                return;
            }

            bool success = await worldRegistry.CreateWorldAsync(worldName);
            if (success)
            {
                logger.LogInfo($"Мир создан: {worldName}");
                ShowCreateWorldPanel(false);
                OnEditWorldSettings(worldName);
            }
            else
            {
                logger.LogError($"Не удалось создать мир: {worldName}");
            }
        }

        public void OnEndEditWorld()
        {
            ShowCreateWorldPanel(false);
            RefreshWorldsList();
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
            logger.LogInfo($"Confirmation: {message}");
            await UniTask.Yield();
            return true;
        }
        public void RefreshWorldsList()
        {
            UpdateWorldsList(default);
        }

        private void OnDestroy()
        {
            disposables?.Dispose();
            
            if (worldRegistry != null)
            {
                worldRegistry.AvailableWorlds.CollectionChanged -= UpdateWorldsList;
            }
        }
    }
}