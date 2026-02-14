using UnityEngine;

namespace TheRavine.InventoryElements
{
    public class UIInventorySlot : MonoBehaviour
    {
        public RectTransform rectTransform;
        public UIInventoryItem _uiInventoryItem;
        public InventorySlot slot { get; private set; }
        public int index;
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