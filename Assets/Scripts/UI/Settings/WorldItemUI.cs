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
        [SerializeField] private TextMeshProUGUI worldNameText, lastSaveTimeText, cycleCountText;
        [SerializeField] private Button enterWorldButton, deleteWorldButton, editSettingsButton;
        [SerializeField] private Image worldIcon;
        
        [Header("Icons")]
        [SerializeField] private Sprite defaultWorldIcon, currentWorldIcon;

        private string worldName;
        private Action onEnterWorld, onDeleteWorld, onEditSettings;
        private RavineLogger logger;
        private WorldRegistry worldRegistry;

        public async void Initialize(
            string worldName, 
            Action onEnterWorld, 
            Action onDeleteWorld, 
            Action onEditSettings, 
            RavineLogger logger, 
            WorldRegistry worldRegistry)
        {
            this.logger = logger;
            this.worldRegistry = worldRegistry;
            this.worldName = worldName;
            this.onEnterWorld = onEnterWorld;
            this.onDeleteWorld = onDeleteWorld;
            this.onEditSettings = onEditSettings;

            SetupUI();
            BindButtons();
            await UpdateWorldInfoAsync();
        }

        private void SetupUI()
        {
            if (worldNameText != null)
                worldNameText.text = worldName;
            
            if (cycleCountText != null)
                UpdateCycleCount(0);
        }

        private void BindButtons()
        {
            enterWorldButton?.onClick.AddListener(() => onEnterWorld?.Invoke());
            deleteWorldButton?.onClick.AddListener(() => onDeleteWorld?.Invoke());
            editSettingsButton?.onClick.AddListener(() => onEditSettings?.Invoke());
        }

        private async UniTask UpdateWorldInfoAsync()
        {
            try
            {
                if (!await worldRegistry.ExistsAsync(worldName))
                {
                    SetErrorState("Данные недоступны");
                    return;
                }

                WorldState worldState = worldRegistry.GetCurrentState();
                WorldConfiguration worldConfiguration = worldRegistry.GetCurrentConfig();
                
                UpdateWorldNameDisplay(worldConfiguration.worldName);
                UpdateLastSaveTime(worldState.lastSaveTime);
                UpdateCycleCount(worldState.cycleCount);
                UpdateWorldIcon();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Не удалось загрузить информацию о мире {worldName}: {ex.Message}");
                SetErrorState("Ошибка загрузки");
            }
        }

        private void UpdateWorldNameDisplay(string displayName)
        {
            if (worldNameText == null) return;
            
            bool isCurrent = worldRegistry.CurrentWorldName == worldName;
            worldNameText.text = isCurrent ? $"{displayName} (Текущий)" : displayName;
        }

        private void UpdateLastSaveTime(long lastSaveTime)
        {
            if (lastSaveTimeText == null) return;

            if (lastSaveTime > 0)
            {
                var saveTime = DateTimeOffset.FromUnixTimeSeconds(lastSaveTime);
                lastSaveTimeText.text = FormatLastSaveTime(saveTime);
            }
            else
            {
                lastSaveTimeText.text = "Новый мир";
            }
        }

        private void UpdateCycleCount(int cycleCount)
        {
            if (cycleCountText != null)
            {
                cycleCountText.text = $"Циклов: {cycleCount}";
            }
        }

        private void UpdateWorldIcon()
        {
            if (worldIcon == null) return;

            bool isCurrent = worldRegistry.CurrentWorldName == worldName;
            worldIcon.sprite = isCurrent ? currentWorldIcon : defaultWorldIcon;
        }

        private string FormatLastSaveTime(DateTimeOffset saveTime)
        {
            var now = DateTimeOffset.Now;
            var diff = now - saveTime;
            
            if (diff.TotalMinutes < 1) return "Только что";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} мин назад";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} ч назад";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} дн назад";
            
            return saveTime.ToString("dd.MM.yyyy");
        }

        private void SetErrorState(string message)
        {
            if (lastSaveTimeText != null)
                lastSaveTimeText.text = message;
            
            if (cycleCountText != null)
                cycleCountText.text = "";
        }

        private void OnDestroy()
        {
            enterWorldButton?.onClick.RemoveAllListeners();
            deleteWorldButton?.onClick.RemoveAllListeners();
            editSettingsButton?.onClick.RemoveAllListeners();
        }
    }
}