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

        private string _worldName;
        private Action _onEnterWorld, _onDeleteWorld, _onEditSettings;
        private IRavineLogger _logger;
        private WorldStorage _worldStorage;
        private WorldRegistry _worldRegistry;

        public async void Initialize(
            string worldName, 
            Action onEnterWorld, 
            Action onDeleteWorld, 
            Action onEditSettings, 
            IRavineLogger logger, 
            WorldStorage worldStorage,
            WorldRegistry worldRegistry)
        {
            _logger = logger;
            _worldStorage = worldStorage;
            _worldRegistry = worldRegistry;
            _worldName = worldName;
            _onEnterWorld = onEnterWorld;
            _onDeleteWorld = onDeleteWorld;
            _onEditSettings = onEditSettings;

            SetupUI();
            BindButtons();
            await UpdateWorldInfoAsync();
        }

        private void SetupUI()
        {
            if (worldNameText != null)
                worldNameText.text = _worldName;
            
            if (cycleCountText != null)
                UpdateCycleCount(0);
        }

        private void BindButtons()
        {
            enterWorldButton?.onClick.AddListener(() => _onEnterWorld?.Invoke());
            deleteWorldButton?.onClick.AddListener(() => _onDeleteWorld?.Invoke());
            editSettingsButton?.onClick.AddListener(() => _onEditSettings?.Invoke());
        }

        private async UniTask UpdateWorldInfoAsync()
        {
            try
            {
                if (!await _worldStorage.ExistsAsync(_worldName))
                {
                    SetErrorState("Данные недоступны");
                    return;
                }

                var (worldData, worldSettings) = await _worldStorage.LoadFullAsync(_worldName);
                
                UpdateWorldNameDisplay(worldSettings.worldName);
                UpdateLastSaveTime(worldData.lastSaveTime);
                UpdateCycleCount(worldData.cycleCount);
                UpdateWorldIcon();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не удалось загрузить информацию о мире {_worldName}: {ex.Message}");
                SetErrorState("Ошибка загрузки");
            }
        }

        private void UpdateWorldNameDisplay(string displayName)
        {
            if (worldNameText == null) return;
            
            bool isCurrent = _worldRegistry.CurrentWorldName == _worldName;
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

            bool isCurrent = _worldRegistry.CurrentWorldName == _worldName;
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