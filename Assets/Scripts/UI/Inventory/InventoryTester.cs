using System.Collections.Generic;
using Random = TheRavine.Extensions.RavineRandom;
using ZLinq;

using TheRavine.InventoryElements;

namespace TheRavine.Inventory
{
    public class InventoryTester
    {
        private readonly UIInventorySlot[] uiSlots;
        private readonly EventDrivenInventoryProxy inventory;
        private readonly InfoManager infoManager;

        public InventoryTester(UIInventorySlot[] uiSlots, InfoManager infoManager, EventDrivenInventoryProxy proxy)
        {
            this.uiSlots = uiSlots;
            this.infoManager = infoManager;

            this.inventory = proxy;
            inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
        }

        private void FillRandomItems(List<string> titles, int itemsPerTitle)
        {
            IInventorySlot[] inventorySlots = inventory.GetAllSlots();
            var freeSlots = inventorySlots
                .AsValueEnumerable()
                .Select((slot, idx) => slot.isEmpty ? idx : -1)
                .Where(idx => idx >= 0)
                .ToList();

            foreach (var title in titles)
            {
                for (int i = 0; i < itemsPerTitle && freeSlots.Count > 0; i++)
                {
                    int randIndex = Random.RangeInt(0, freeSlots.Count);
                    int slotIdx = freeSlots[randIndex];
                    freeSlots.RemoveAt(randIndex);

                    var item = infoManager.GetInventoryItem(title, Random.RangeInt(1, 10));

                    if (item != null)
                    {
                        inventory.TryToAddSlot(this, inventorySlots[slotIdx], item);
                    }
                }
            }
            
            RefreshAllSlots();
        }

        public void FillSlots(bool filling)
        {
            if (!filling) return;

            FillRandomItems(
                new List<string> {
                    nameof(Porchini), nameof(Toadstool),
                    nameof(BrownBoletus), nameof(Boletus), nameof(Battery),
                    nameof(Politics), nameof(Kirieshky)
                },
                itemsPerTitle: 5
            );
            FillRandomItems(
                new List<string> { nameof(Isa), nameof(AndrewNovichenko), nameof(Jirinovskiy) },
                itemsPerTitle: 2
            );
        }

        public void SetDataFromSerializableList(SerializableList<SerializableInventorySlot> data)
        {
            if (data == null) return;
            foreach (var slotInfo in data.list)
            {
                if (slotInfo.title == "empty") continue;
                var item = infoManager.GetInventoryItem(slotInfo.title, slotInfo.amount);
                if (item != null)
                    inventory.TryToAdd(this, item);
            }
            RefreshAllSlots();
        }

        public SerializableList<SerializableInventorySlot> Serialize() => InventorySerializer.Serialize(inventory);

        private void OnInventoryStateChanged(object sender)
        {
            RefreshAllSlots();
        }
        private void RefreshAllSlots()
        {
            var slots = inventory.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                uiSlots[i].SetSlot(slots[i], i);
                uiSlots[i].Refresh();
            }
        }

        public void Dispose()
        {
            inventory.OnInventoryStateChangedEvent -= OnInventoryStateChanged;
        }
    }
}