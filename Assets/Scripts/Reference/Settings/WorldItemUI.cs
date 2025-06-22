using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class WorldItemUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI worldNameText;
        [SerializeField] private TextMeshProUGUI lastSaveTimeText;
        [SerializeField] private Button enterWorldButton;
        [SerializeField] private Button deleteWorldButton;
        [SerializeField] private Button editSettingsButton;
        [SerializeField] private Image worldIcon;
        
        [Header("Icons")]
        [SerializeField] private Sprite defaultWorldIcon;
        [SerializeField] private Sprite currentWorldIcon;
        
        private string _worldName;
        private Action _onEnterWorld;
        private Action _onDeleteWorld;
        private Action _onEditSettings;
        private ILogger logger;
        private IWorldService worldService;
        public void Initialize(string worldName, Action onEnterWorld, Action onDeleteWorld, Action onEditSettings, ILogger logger, IWorldService worldService)
        {
            this.logger = logger;
            this.worldService = worldService;
            _worldName = worldName;
            _onEnterWorld = onEnterWorld;
            _onDeleteWorld = onDeleteWorld;
            _onEditSettings = onEditSettings;

            SetupUI();
            BindButtons();
            UpdateWorldInfo().Forget();
        }

        private void SetupUI()
        {
            if (worldNameText != null)
                worldNameText.text = _worldName;
            
            var worldManager = ServiceLocator.GetService<IWorldManager>();
            bool isCurrentWorld = worldManager?.CurrentWorldName == _worldName;
            
            if (worldIcon != null)
            {
                worldIcon.sprite = isCurrentWorld ? currentWorldIcon : defaultWorldIcon;
                worldIcon.color = isCurrentWorld ? Color.green : Color.white;
            }
            
            if (enterWorldButton != null)
            {
                enterWorldButton.interactable = !isCurrentWorld;
                var buttonText = enterWorldButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = isCurrentWorld ? "Текущий" : "Войти";
            }
        }

        private void BindButtons()
        {
            enterWorldButton?.onClick.AddListener(() => _onEnterWorld?.Invoke());
            deleteWorldButton?.onClick.AddListener(() => _onDeleteWorld?.Invoke());
            editSettingsButton?.onClick.AddListener(() => _onEditSettings?.Invoke());
        }

        private async UniTask UpdateWorldInfo()
        {
            if (lastSaveTimeText == null) return;

            try
            {
                if (await worldService.ExistsAsync(_worldName))
                {
                    var worldData = await worldService.LoadDataAsync(_worldName);
                    if (worldData.lastSaveTime > 0)
                    {
                        var saveTime = DateTimeOffset.FromUnixTimeSeconds(worldData.lastSaveTime);
                        lastSaveTimeText.text = $"Сохранен: {saveTime:dd.MM.yy HH:mm}";
                    }
                    else
                    {
                        lastSaveTimeText.text = "Новый мир";
                    }
                }
                else
                {
                    lastSaveTimeText.text = "Данные недоступны";
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Не удалось загрузить информацию о мире {_worldName}: {ex.Message}");
                lastSaveTimeText.text = "Ошибка загрузки";
            }
        }

        private void OnDestroy()
        {
            enterWorldButton?.onClick.RemoveAllListeners();
            deleteWorldButton?.onClick.RemoveAllListeners();
            editSettingsButton?.onClick.RemoveAllListeners();
        }
    }
}