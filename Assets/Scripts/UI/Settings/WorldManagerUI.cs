using System;
using System.Collections.Generic;
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

        [Header("Подтверждение удаления")]
        [SerializeField] private GameObject confirmDeletePanel;
        [SerializeField] private Button confirmDeleteButton;
        [SerializeField] private Button cancelDeleteButton;
        [SerializeField] private TMP_Text confirmDeleteText;
        
        private RavineLogger _logger;
        private WorldRegistry _worldRegistry;
        private CompositeDisposable _disposables = new();
        private readonly Dictionary<string, WorldItemUI> _worldItems = new();
        private UniTaskCompletionSource<bool> _deleteConfirmation;

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

            if (confirmDeleteButton != null)
                confirmDeleteButton.onClick.AddListener(OnConfirmDelete);
            if (cancelDeleteButton != null)
                cancelDeleteButton.onClick.AddListener(OnCancelDelete);
            if (confirmDeletePanel != null)
                confirmDeletePanel.SetActive(false);
            
            ShowPanel(PanelType.WorldList);
        }

        private void BindToModel()
        {
            _worldRegistry.AvailableWorlds.CollectionChanged += OnWorldsListChanged;

            RebuildWorldsList();
            
            _worldRegistry.CurrentWorldId
                .Skip(1)
                .Subscribe(_ => RefreshWorldItems())
                .AddTo(_disposables);
        }

        private void OnWorldsListChanged(in NotifyCollectionChangedEventArgs<string> e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.IsSingleItem:
                    AddWorldItem(e.NewItem, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove when e.IsSingleItem:
                    RemoveWorldItem(e.OldItem);
                    break;
                default:
                    RebuildWorldsList();
                    break;
            }
        }

        private void RebuildWorldsList()
        {
            foreach (var item in _worldItems.Values)
            {
                DestroyWorldItem(item);
            }
            _worldItems.Clear();
            
            foreach (var worldName in _worldRegistry.AvailableWorlds)
            {
                AddWorldItem(worldName, -1);
            }
        }

        private void RefreshWorldItems()
        {
            foreach (var item in _worldItems.Values)
            {
                item.Refresh();
            }
        }

        private void AddWorldItem(string worldName, int index)
        {
            if (_worldItems.ContainsKey(worldName)) return;

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
                _worldItems.Add(worldName, worldItemComponent);

                if (index >= 0 && index < worldsContainer.childCount)
                    worldItem.transform.SetSiblingIndex(index);
            }
            else
            {
                Destroy(worldItem);
            }
        }

        private void RemoveWorldItem(string worldName)
        {
            if (!_worldItems.Remove(worldName, out var item)) return;
            DestroyWorldItem(item);
        }

        private void DestroyWorldItem(WorldItemUI item)
        {
            if (item == null) return;
            item.transform.SetParent(null, false);
            Destroy(item.gameObject);
        }

        private async void OnEnterWorld(string worldName)
        {
            try
            {
                bool success = await EnterWorldAndStartGameAsync(worldName);
                if (!success)
                {
                    _logger.LogError($"Не удалось войти в мир: {worldName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
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
            if (!await ShowDeleteConfirmationAsync($"Удалить мир '{worldName}'?"))
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
            try
            {
                string worldName = newWorldNameInput.text.Trim();
                
                if (!WorldRegistry.IsValidWorldName(worldName))
                {
                    _logger.LogWarning($"Недопустимое имя мира: '{worldName}'");
                    return;
                }

                if (_worldRegistry.AvailableWorlds.Contains(worldName))
                {
                    _logger.LogWarning($"Мир с именем '{worldName}' уже существует");
                    return;
                }

                if (!await _worldRegistry.CreateWorldAsync(worldName))
                {
                    _logger.LogError($"Не удалось создать мир: {worldName}");
                    return;
                }

                if (!await _worldRegistry.LoadWorldAsync(worldName))
                {
                    _logger.LogError($"Не удалось загрузить созданный мир: {worldName}");
                    return;
                }

                _editingWorldId = worldName;
                ShowPanel(PanelType.EditWorld);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
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

        private UniTask<bool> ShowDeleteConfirmationAsync(string message)
        {
            if (confirmDeletePanel == null)
            {
                _logger.LogWarning("Панель подтверждения удаления не назначена");
                return UniTask.FromResult(false);
            }

            _deleteConfirmation?.TrySetResult(false);
            _deleteConfirmation = new UniTaskCompletionSource<bool>();

            if (confirmDeleteText != null)
                confirmDeleteText.text = message;
            confirmDeletePanel.SetActive(true);

            return _deleteConfirmation.Task;
        }

        private void OnConfirmDelete() => ResolveDeleteConfirmation(true);

        private void OnCancelDelete() => ResolveDeleteConfirmation(false);

        private void ResolveDeleteConfirmation(bool result)
        {
            if (confirmDeletePanel != null)
                confirmDeletePanel.SetActive(false);

            var confirmation = _deleteConfirmation;
            _deleteConfirmation = null;
            confirmation?.TrySetResult(result);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();

            if (confirmDeleteButton != null)
                confirmDeleteButton.onClick.RemoveListener(OnConfirmDelete);
            if (cancelDeleteButton != null)
                cancelDeleteButton.onClick.RemoveListener(OnCancelDelete);
            _deleteConfirmation?.TrySetResult(false);
            
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