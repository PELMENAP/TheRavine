using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

using TheRavine.EntityControl;
using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIItemContextMenu : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button useButton, splitButton, unequipButton, amountMinusButton, amountPlusButton;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private GameObject inventoryRoot;

        private EventDrivenInventoryProxy inventory;
        private AEntity entity;
        private Canvas canvas;
        private RectTransform canvasRect;
        private UIInventorySlot target;
        private int splitAmount;

        public bool IsOpen => target != null;

        public bool ContainsScreenPoint(Vector2 screenPosition) =>
            IsOpen && panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPosition, UICamera);

        public void SetUp(EventDrivenInventoryProxy inventory, AEntity entity)
        {
            this.inventory = inventory;
            this.entity = entity;

            if (panel != null)
            {
                canvas = panel.GetComponentInParent<Canvas>();
                if (canvas != null) canvasRect = canvas.rootCanvas.transform as RectTransform;
                panel.gameObject.SetActive(false);
            }

            if (useButton != null) useButton.onClick.AddListener(OnUse);
            if (splitButton != null) splitButton.onClick.AddListener(OnSplit);
            if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequip);
            if (amountMinusButton != null) amountMinusButton.onClick.AddListener(OnAmountMinus);
            if (amountPlusButton != null) amountPlusButton.onClick.AddListener(OnAmountPlus);
        }

        public void TearDown()
        {
            if (useButton != null) useButton.onClick.RemoveListener(OnUse);
            if (splitButton != null) splitButton.onClick.RemoveListener(OnSplit);
            if (unequipButton != null) unequipButton.onClick.RemoveListener(OnUnequip);
            if (amountMinusButton != null) amountMinusButton.onClick.RemoveListener(OnAmountMinus);
            if (amountPlusButton != null) amountPlusButton.onClick.RemoveListener(OnAmountPlus);
            Close();
            inventory = null;
            entity = null;
        }

        public void Open(UIInventorySlot slotUI, Vector2 screenPosition)
        {
            if (panel == null || inventory == null || slotUI == null) return;
            var slot = slotUI.slot;
            if (slot == null || slot.isEmpty) { Close(); return; }

            bool storage = slot.kind.IsStorage();
            bool canUse = storage && slot.item is ItemClass itemClass && itemClass.IsUsable;
            bool canSplit = storage && slot.amount > 1;
            bool canUnequip = slot.kind.IsEquipment();
            if (!canUse && !canSplit && !canUnequip) { Close(); return; }

            target = slotUI;
            splitAmount = Mathf.Clamp(slot.amount / 2, 1, Mathf.Max(1, slot.amount - 1));

            SetActive(useButton, canUse);
            SetActive(splitButton, canSplit);
            SetActive(amountMinusButton, canSplit);
            SetActive(amountPlusButton, canSplit);
            SetActive(unequipButton, canUnequip);
            if (amountText != null)
            {
                amountText.gameObject.SetActive(canSplit);
                amountText.text = splitAmount.ToString();
            }

            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            PlaceAt(screenPosition);
        }

        public void Close()
        {
            target = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        private Camera UICamera =>
            canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;

        private void PlaceAt(Vector2 screenPosition)
        {
            if (canvasRect == null)
            {
                panel.position = screenPosition;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, UICamera, out Vector2 local))
                return;

            Vector2 relativeScale = new Vector2(
                panel.lossyScale.x / canvasRect.lossyScale.x,
                panel.lossyScale.y / canvasRect.lossyScale.y);
            Vector2 size = Vector2.Scale(panel.rect.size, relativeScale);
            Vector2 pivot = panel.pivot;
            Rect bounds = canvasRect.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + size.x * pivot.x, Mathf.Max(bounds.xMin + size.x * pivot.x, bounds.xMax - size.x * (1f - pivot.x)));
            local.y = Mathf.Clamp(local.y, bounds.yMin + size.y * pivot.y, Mathf.Max(bounds.yMin + size.y * pivot.y, bounds.yMax - size.y * (1f - pivot.y)));
            panel.position = canvasRect.TransformPoint(local);
        }

        private void Update()
        {
            if (target == null) return;

            if (inventoryRoot != null && !inventoryRoot.activeInHierarchy)
            {
                Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !ContainsScreenPoint(mouse.position.ReadValue()))
                Close();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnAmountMinus() => SetSplitAmount(splitAmount - 1);
        private void OnAmountPlus() => SetSplitAmount(splitAmount + 1);

        private void SetSplitAmount(int value)
        {
            if (target == null || target.slot == null || target.slot.isEmpty) { Close(); return; }
            splitAmount = Mathf.Clamp(value, 1, Mathf.Max(1, target.slot.amount - 1));
            if (amountText != null) amountText.text = splitAmount.ToString();
        }

        private void OnUse()
        {
            var slot = target != null ? target.slot : null;
            Close();
            if (slot == null || slot.isEmpty || inventory == null) return;
            if (slot.item is ItemClass itemClass && itemClass.IsUsable &&
                itemClass.TryUse(new UseContext { Entity = entity, Inventory = inventory }))
                inventory.ConsumeFromSlot(this, slot, 1);
        }

        private void OnSplit()
        {
            var slot = target != null ? target.slot : null;
            int amount = splitAmount;
            Close();
            if (slot == null || inventory == null) return;
            inventory.TrySplit(this, slot, amount, out _);
        }

        private void OnUnequip()
        {
            var slot = target != null ? target.slot : null;
            Close();
            if (slot == null || inventory == null) return;
            inventory.TryUnequip(this, slot);
        }
    }
}
