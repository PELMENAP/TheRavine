using UnityEngine;

namespace TheRavine.InventoryElements
{
    public class UIInventorySlot : MonoBehaviour
    {
        public RectTransform rectTransform;
        public UIInventoryItem _uiInventoryItem;
        public IInventorySlot slot { get; private set; }
        public int index;
        public void SetSlot(IInventorySlot newSlot, int index)
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