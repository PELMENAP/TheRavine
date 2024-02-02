using UnityEngine;

namespace TheRavine.InventoryElements
{
    public class UIInventorySlot : MonoBehaviour
    {
        public UIInventoryItem _uiInventoryItem;
        public IInventorySlot slot { get; private set; }
        public void SetSlot(IInventorySlot newSlot)
        {
            slot = newSlot;
        }
        public void Refresh()
        {
            if (slot != null)
                _uiInventoryItem.Refresh(slot);
        }
    }
}