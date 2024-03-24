using System.Collections.Generic;
using Random = TheRavine.Extentions.RavineRandom;

using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIInventoryTester
    {
        private UIInventorySlot[] _uiSlots;

        public InventoryWithSlots inventory { get; }

        public UIInventoryTester(UIInventorySlot[] uislots)
        {
            _uiSlots = uislots;

            inventory = new InventoryWithSlots(uislots.Length);
            inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
        }

        public void FillSlots(bool filling)
        {
            if (filling)
            {
                var allSlots = inventory.GetAllSlots();
                var availableSlots = new List<IInventorySlot>(allSlots);
                var filledSlots = 10;
                for (int i = 0; i < filledSlots; i++)
                {
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = InfoManager.GetInventoryItem("porchini", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }
            }
            SetupInventoryUI(inventory);
        }

        private void SetupInventoryUI(InventoryWithSlots inventory)
        {
            var allSlots = inventory.GetAllSlots();
            var allSlotsCount = allSlots.Length;
            for (int i = 0; i < allSlotsCount; i++)
            {
                var slot = allSlots[i];
                var uiSlot = _uiSlots[i];
                uiSlot.SetSlot(slot);
                uiSlot.Refresh();
            }
        }

        private void OnInventoryStateChanged(object sender)
        {
            foreach (var uiSlot in _uiSlots)
            {
                uiSlot.Refresh();
            }
        }
    }
}