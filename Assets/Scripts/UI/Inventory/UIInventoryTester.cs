using System.Collections.Generic;
using Random = TheRavine.Extentions.RavineRandom;

using TheRavine.InventoryElements;
using Unity.Android.Gradle.Manifest;

namespace TheRavine.Inventory
{
    public class UIInventoryTester
    {
        private UIInventorySlot[] _uiSlots;

        public InventoryWithSlots inventory { get; }
        public InfoManager infoManager;

        public UIInventoryTester(UIInventorySlot[] uislots, DataItems dataItems)
        {
            _uiSlots = uislots;

            infoManager = new InfoManager(dataItems);
            inventory = new InventoryWithSlots(uislots.Length);
            inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
        }

        public void FillSlots(bool filling)
        {
            if (filling)
            {
                var allSlots = inventory.GetAllSlots();
                var availableSlots = new List<IInventorySlot>(allSlots);
                if(availableSlots.Count == 0) return;
                var filledSlots = 5;
                for (int i = 0; i < filledSlots; i++)
                {
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("porchini", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("toadstool", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("brownboletus", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("boletus", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("battery", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("politics", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem("kirieshky", Random.RangeInt(1, 10));
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }
            }
            SetupInventoryUI(inventory);
        }

        public void SetDataFromSerializableList(SerializableList<SerializableInventorySlot> data)
        {
            if(data == null) return;
            var allSlots = inventory.GetAllSlots();
            for(int i = 0; i < inventory.capacity; i++)
            {
                var dataItem = data.list[i];
                if(dataItem.title == "the ravine") continue;
                var item = infoManager.GetInventoryItem(dataItem.title, dataItem.amount);
                inventory.TryToAddSlot(this, allSlots[i], item);
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