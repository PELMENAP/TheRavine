using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine;

using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIDragger : MonoBehaviour
    {
        [SerializeField] private InputActionReference point;
        [SerializeField] private UIInventory _uiInventory;
        [SerializeField] private Canvas _mainCanvas;
        private EventSystem eventSystem;
        private Mouse mouse;
        private bool isDragging = false;
        private List<RaycastResult> results = new List<RaycastResult>();
        private PointerEventData eventData;
        private Vector2 startPosition;
        private UIInventorySlot lastSlot;

        private void Start()
        {
            eventData = new PointerEventData(eventSystem);
            mouse = Mouse.current;
            eventSystem = EventSystem.current;
            point.action.performed += OnDrag;
        }

        private void OnDrag(InputAction.CallbackContext context)
        {
            if (mouse.leftButton.wasReleasedThisFrame && lastSlot != null)
            {
                isDragging = false;
                eventData.position = mouse.position.ReadValue();
                startPosition = mouse.position.ReadValue();
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

        private async UniTaskVoid Dragging()
        {
            while (isDragging)
            {
                lastSlot._uiInventoryItem._rectTransform.position = mouse.position.ReadValue();
                await UniTask.WaitForFixedUpdate();
            }
            await UniTask.WaitForFixedUpdate();
        }
    }
}