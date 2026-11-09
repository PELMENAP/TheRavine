using UnityEngine;

namespace TheRavine.InventoryElements
{
    public class UIInventorySlot : MonoBehaviour
    {
        public RectTransform rectTransform;
        public UIInventoryItem _uiInventoryItem;
        public InventorySlot slot { get; private set; }
        public int index;
        [SerializeField] private SlotKind kind = SlotKind.Bag;
        public SlotKind Kind => kind;
        public void SetKind(SlotKind newKind) => kind = newKind;
        public void SetSlot(InventorySlot newSlot, int index)
        {
            slot = newSlot;
            this.index = index;
        }
        public void Refresh()
        {
            if (slot != null)
                _uiInventoryItem.Refresh(slot);
        }
    }
}