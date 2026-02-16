using System.Collections.Generic;
using Random = TheRavine.Extensions.RavineRandom;
using ZLinq;

using TheRavine.InventoryElements;
using System.Linq;

namespace TheRavine.Inventory
{
    public class InventoryTester
    {
        private readonly UIInventorySlot[] uiSlots;
        private EventDrivenInventoryProxy inventoryProxy;
        private readonly InfoManager infoManager;

        public InventoryTester(UIInventorySlot[] uiSlots, InfoManager infoManager, EventDrivenInventoryProxy inventoryProxy)
        {
            this.uiSlots = uiSlots;
            this.infoManager = infoManager;

            this.inventoryProxy = inventoryProxy;
            inventoryProxy.OnInventoryStateChangedEvent += OnInventoryStateChanged;
        }

        private void FillRandomItems(List<string> titles, int itemsPerTitle)
        {
            InventorySlot[] inventorySlots = inventoryProxy.GetAllSlots();
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
                        inventoryProxy.TryToAddSlot(this, inventorySlots[slotIdx], item);
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

        public void SetDataFromSerializableList(SerializableInventorySlot[] data)
        {
            List<InventorySlot> inventorySlots = new();

            if (data == null) return;

            for (int i = 0; i < data.Length; i++)
            {
                var slot = new InventorySlot();

                if (data[i].title != "empty")
                {
                    var item = infoManager.GetInventoryItem(data[i].title, data[i].amount);
                    if (item != null)
                        slot.SetItem(item);
                }

                inventorySlots.Add(slot);   
            }

            inventoryProxy = new(inventorySlots);
            RefreshAllSlots();
        }

        public SerializableInventorySlot[] Serialize() => inventoryProxy.GetSerializable();

        private void OnInventoryStateChanged(object sender)
        {
            RefreshAllSlots();
        }
        private void RefreshAllSlots()
        {
            var slots = inventoryProxy.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                uiSlots[i].SetSlot(slots[i], i);
                uiSlots[i].Refresh();
            }
        }

        public void Dispose()
        {
            inventoryProxy.OnInventoryStateChangedEvent -= OnInventoryStateChanged;
        }
    }
}