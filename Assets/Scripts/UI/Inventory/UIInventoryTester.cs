using System.Collections.Generic;
using Random = TheRavine.Extensions.RavineRandom;

using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class UIInventoryTester
    {
        private UIInventorySlot[] _uiSlots;

        public InventoryModel inventory { get; }
        public bool HasItem(string title) => inventory.HasItem(GetInventoryItem(title).type);
        public IInventoryItem GetInventoryItem(string title, int amount = 1) => infoManager.GetInventoryItem(title, amount);
        public InfoManager infoManager;

        public UIInventoryTester(UIInventorySlot[] uislots, DataItems dataItems)
        {
            _uiSlots = uislots;

            infoManager = new InfoManager(dataItems);
            inventory = new InventoryModel(uislots.Length);
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
                    var item = infoManager.GetInventoryItem(nameof(Porchini), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Toadstool), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(BrownBoletus), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Boletus), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Battery), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Politics), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < filledSlots; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Kirieshky), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }

                for (int i = 0; i < 2; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Isa), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }
                for (int i = 0; i < 2; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(AndrewNovichenko), Random.RangeInt(1, 10));
                    if(item == null) continue;
                    inventory.TryToAddSlot(this, rSlot, item);
                    availableSlots.Remove(rSlot);
                }
                for (int i = 0; i < 2; i++)
                {
                    if(availableSlots.Count == 0) return;
                    var rSlot = availableSlots[Random.RangeInt(0, availableSlots.Count - 1)];
                    var item = infoManager.GetInventoryItem(nameof(Jirinovskiy), Random.RangeInt(1, 10));
                    if(item == null) continue;
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

        private void SetupInventoryUI(InventoryModel inventory)
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