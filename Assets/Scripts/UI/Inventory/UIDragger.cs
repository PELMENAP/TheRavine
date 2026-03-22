using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using TMPro;

using TheRavine.Base;
using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIDragger : MonoBehaviour
    {
        [SerializeField] private InputActionReference point, leftclick;
        [SerializeField] private UIInventory _uiInventory;
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private TextMeshProUGUI text, title;
        [SerializeField] private Image image;
        private EventSystem eventSystem;
        private Mouse mouse;
        private bool isDragging = false;
        private readonly List<RaycastResult> results = new(10);
        private PointerEventData eventData;
        private UIInventorySlot lastSlot;
        private GlobalSettings globalSettings;
        private EventDrivenInventoryProxy inventoryModel;
        private LLMItemDescriptionService _llmDescriptionService;
        private ItemDescriptionRegistry _descriptionRegistry;
        private MainComponent playerMainComponent;

        public void SetUp(
            EventDrivenInventoryProxy inventoryModel,
            ItemDescriptionRegistry descriptionRegistry,
            LLMItemDescriptionService llmDescriptionService,
            MainComponent playerMainComponent)
        {
            this.inventoryModel = inventoryModel;
            _descriptionRegistry = descriptionRegistry;
            _llmDescriptionService = llmDescriptionService;
            this.playerMainComponent = playerMainComponent;

            _llmDescriptionService.OnDescriptionStarted += OnLLMDescriptionStarted;
            _llmDescriptionService.OnDescriptionToken   += OnLLMDescriptionToken;

            globalSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            eventSystem = EventSystem.current;
            eventData = new PointerEventData(eventSystem);
            mouse = Mouse.current;

            switch (globalSettings.controlType)
            {
                case ControlType.Personal:
                    leftclick.action.performed += OnDragPC;
                    break;
                case ControlType.Mobile:
                    point.action.performed += OnDragMobile;
                    break;
            }
        }
        
        private const float DescriptionStalenessSeconds = 300f;
        private void ShowItemInfo(UIInventorySlot slot)
        {
            var item = slot.slot.item;

            text.text = _descriptionRegistry.Get(item);
            title.text = item.info.title;
            image.sprite = item.info.infoSprite;

            if (!_descriptionRegistry.IsStale(item, DescriptionStalenessSeconds))
                return;

            var playerCtx = ResolvePlayerContext();
            _llmDescriptionService.RequestDescription(item, playerCtx);
        }
        
        private void OnLLMDescriptionStarted()
        {
            if (lastSlot != null && !lastSlot.slot.isEmpty)
                text.text = string.Empty;
        }

        private void OnLLMDescriptionToken(string token)
        {
            if (lastSlot != null && !lastSlot.slot.isEmpty)
                text.text = token;
        }
        private PlayerContext ResolvePlayerContext()
        {
            string[] facts =  { "Хочет знать как устроен мир", "Слышал, что кириешки можно использовать для розжига", };

            return new PlayerContext
            {
                Name = playerMainComponent.GetEntityName(),
                ProfessionId = "слесарь 4 разряда",
                Expertise = 0.7f,
                Doubt = 0.3f,
                KnownFacts = facts,
            };
        }


        private void OnDragPC(InputAction.CallbackContext context)
        {
            if(_uiInventory == null) return;
            if (mouse.leftButton.wasReleasedThisFrame && lastSlot != null)
            {
                isDragging = false;
                results.Clear();
                eventData.position = mouse.position.ReadValue();
                eventSystem.RaycastAll(eventData, results);
                for (int i = 0; i < results.Count; i++)
                {
                    var otherSlotUI = (UIInventorySlot)results[i].gameObject.GetComponent("UIInventorySlot");
                    if (otherSlotUI == null) continue;
                    if(_uiInventory == null) return;
                    inventoryModel.TransitFromSlotToSlot(this, lastSlot.slot, otherSlotUI.slot);
                    lastSlot.Refresh();
                    otherSlotUI.Refresh();
                    break;
                }
                lastSlot._uiInventoryItem.transform.localPosition = Vector3.zero;
                lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = true;
            }
            else
            {
                isDragging = true;
                eventData.position = mouse.position.ReadValue();
                eventSystem.RaycastAll(eventData, results);
                for (int i = 0; i < results.Count; i++)
                {
                    // Debug.Log("Hit UI element: " + result.gameObject);
                    lastSlot = (UIInventorySlot)results[i].gameObject.GetComponent("UIInventorySlot");
                    if (lastSlot == null || lastSlot.slot.isEmpty) continue;
                    if(_uiInventory == null) return;
                    var slotTransform = lastSlot._uiInventoryItem._rectTransform.parent;
                    slotTransform.SetAsLastSibling();
                    lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = false;
                    Dragging().Forget();
                    ShowItemInfo(lastSlot);
                    image.sprite = lastSlot.slot.item.info.infoSprite;
                    break;
                }
            }
        }

        private void OnDragMobile(InputAction.CallbackContext context)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        results.Clear();
                        eventData.position = touch.screenPosition;
                        eventSystem.RaycastAll(eventData, results);
                        for (int i = 0; i < results.Count; i++)
                        {
                            // Debug.Log("Hit UI element: " + result.gameObject);
                            lastSlot = (UIInventorySlot)results[i].gameObject.GetComponent("UIInventorySlot");
                            if (lastSlot == null || lastSlot.slot.isEmpty) continue;
                            if(_uiInventory == null) return;
                            var slotTransform = lastSlot._uiInventoryItem._rectTransform.parent;
                            slotTransform.SetAsLastSibling();
                            lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = false;
                            ShowItemInfo(lastSlot);
                            image.sprite = lastSlot.slot.item.info.infoSprite;
                            break;
                        }
                        break;
                    case TouchPhase.Moved:
                        if (lastSlot == null) return;
                        if(_uiInventory == null) return;
                        lastSlot._uiInventoryItem._rectTransform.position = touch.screenPosition;
                        break;
                    case TouchPhase.Ended:
                        if (lastSlot == null) return;
                        if(_uiInventory == null) return;
                        eventData.position = touch.screenPosition;
                        eventSystem.RaycastAll(eventData, results);
                        for (int i = 0; i < results.Count; i++)
                        {
                            var otherSlotUI = (UIInventorySlot)results[i].gameObject.GetComponent("UIInventorySlot");
                            if (otherSlotUI == null)
                                continue;
                            inventoryModel.TransitFromSlotToSlot(this, lastSlot.slot, otherSlotUI.slot);
                            lastSlot.Refresh();
                            otherSlotUI.Refresh();
                            break;
                        }
                        lastSlot._uiInventoryItem.transform.localPosition = Vector3.zero;
                        lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = true;
                        break;
                }
            }
        }

        private async UniTaskVoid Dragging()
        {
            while (isDragging && lastSlot != null)
            {
                lastSlot._uiInventoryItem._rectTransform.position = mouse.position.ReadValue();
                await UniTask.WaitForFixedUpdate();
            }
            await UniTask.WaitForFixedUpdate();
        }

        public void BreakUp()
        {
            if (_llmDescriptionService != null)
            {
                _llmDescriptionService.OnDescriptionStarted -= OnLLMDescriptionStarted;
                _llmDescriptionService.OnDescriptionToken   -= OnLLMDescriptionToken;
            }

            switch (globalSettings.controlType)
            {
                case ControlType.Personal:
                    leftclick.action.performed -= OnDragPC;
                    break;
                case ControlType.Mobile:
                    point.action.performed -= OnDragMobile;
                    break;
            }
        }
    }
}