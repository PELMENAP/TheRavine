using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using TheRavine.Base;
using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIDragger : MonoBehaviour
    {
        [SerializeField] private InputActionReference point, leftclick;
        [SerializeField] private UIInventory _uiInventory;
        [SerializeField] private Canvas _mainCanvas;
        private EventSystem eventSystem;
        private Mouse mouse;
        private bool isDragging = false;
        private List<RaycastResult> results = new List<RaycastResult>();
        private PointerEventData eventData;
        private UIInventorySlot lastSlot;

        private void Start()
        {
            eventSystem = EventSystem.current;
            eventData = new PointerEventData(eventSystem);
            mouse = Mouse.current;

            switch (Settings._controlType)
            {
                case ControlType.Personal:
                    leftclick.action.performed += OnDragPC;
                    break;
                case ControlType.Mobile:
                    point.action.performed += OnDragMobile;
                    break;
            }
        }

        private void OnDragPC(InputAction.CallbackContext context)
        {
            if (mouse.leftButton.wasReleasedThisFrame && lastSlot != null)
            {
                isDragging = false;
                eventData.position = mouse.position.ReadValue();
                eventSystem.RaycastAll(eventData, results);
                for (byte i = 0; i < results.Count; i++)
                {
                    var otherSlotUI = results[i].gameObject.GetComponent<UIInventorySlot>();
                    if (otherSlotUI == null)
                        continue;
                    _uiInventory.inventory.TransitFromSlotToSlot(this, lastSlot.slot, otherSlotUI.slot);
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
                for (byte i = 0; i < results.Count; i++)
                {
                    // Debug.Log("Hit UI element: " + result.gameObject);
                    lastSlot = results[i].gameObject.GetComponent<UIInventorySlot>();
                    if (lastSlot == null || lastSlot.slot.isEmpty)
                        continue;
                    var slotTranform = lastSlot._uiInventoryItem._rectTransform.parent;
                    slotTranform.SetAsLastSibling();
                    lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = false;
                    Dragging().Forget();
                    break;
                }
            }
        }

        private void OnDragMobile(InputAction.CallbackContext context)
        {
            if (!DayCycle.closeThread)
                return;
            foreach (Touch touch in Touch.activeTouches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        eventData.position = touch.screenPosition;
                        eventSystem.RaycastAll(eventData, results);
                        for (byte i = 0; i < results.Count; i++)
                        {
                            // Debug.Log("Hit UI element: " + result.gameObject);
                            lastSlot = results[i].gameObject.GetComponent<UIInventorySlot>();
                            if (lastSlot == null || lastSlot.slot.isEmpty)
                                continue;
                            var slotTranform = lastSlot._uiInventoryItem._rectTransform.parent;
                            slotTranform.SetAsLastSibling();
                            lastSlot._uiInventoryItem._canvasGroup.blocksRaycasts = false;
                            break;
                        }
                        break;
                    case TouchPhase.Moved:
                        if (lastSlot == null)
                            return;
                        lastSlot._uiInventoryItem._rectTransform.position = touch.screenPosition;
                        break;
                    case TouchPhase.Ended:
                        if (lastSlot == null)
                            return;
                        eventData.position = touch.screenPosition;
                        eventSystem.RaycastAll(eventData, results);
                        for (byte i = 0; i < results.Count; i++)
                        {
                            var otherSlotUI = results[i].gameObject.GetComponent<UIInventorySlot>();
                            if (otherSlotUI == null)
                                continue;
                            _uiInventory.inventory.TransitFromSlotToSlot(this, lastSlot.slot, otherSlotUI.slot);
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
            while (isDragging)
            {
                lastSlot._uiInventoryItem._rectTransform.position = mouse.position.ReadValue();
                await UniTask.WaitForFixedUpdate();
            }
            await UniTask.WaitForFixedUpdate();
        }

        private void OnDisable()
        {
            switch (Settings._controlType)
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