using System.Collections.Generic;
using UnityEngine;

using TheRavine.InventoryElements;

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
            var inventorySlots = inventoryProxy.Slots;
            var freeSlots = new List<int>(inventorySlots.Count);
            for (int i = 0; i < inventorySlots.Count; i++)
                if (inventorySlots[i].isEmpty && inventorySlots[i].kind.IsStorage())
                    freeSlots.Add(i);

            foreach (var title in titles)
            {
                for (int i = 0; i < itemsPerTitle && freeSlots.Count > 0; i++)
                {
                    int randIndex = RavineRandom.RangeInt(0, freeSlots.Count);
                    int slotIdx = freeSlots[randIndex];
                    freeSlots.RemoveAt(randIndex);

                    var item = infoManager.GetInventoryItem(title, RavineRandom.RangeInt(1, 10));

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
                    "Porchini", "Toadstool",
                    "BirchBolete", "AspenBolete", "Battery",
                    "Politics", "Kirieshky", "Saffron"
                },
                itemsPerTitle: 5
            );
            FillRandomItems(
                new List<string> { "Isa", "AndrewNovichenko", "Jirinovskiy" },
                itemsPerTitle: 2
            );
        }

        public void SetDataFromSerializableList(SerializableInventorySlot[] data)
        {
            if (data == null) 
            {
                return;
            }

            if (data.Length > uiSlots.Length)
                Debug.LogWarning($"[InventoryTester] Сохранение содержит {data.Length} слотов, UI содержит {uiSlots.Length}: лишние слоты отброшены");

            var inventorySlots = new List<InventorySlot>(uiSlots.Length);

            for (int i = 0; i < uiSlots.Length; i++)
            {
                var slot = new InventorySlot(uiSlots[i].Kind);

                if (i < data.Length)
                {
                    var slotData = data[i];
                    if (slotData != null && !slotData.isEmpty && !string.IsNullOrEmpty(slotData.title) && slotData.amount > 0)
                    {
                        var item = infoManager.GetInventoryItem(slotData.title, slotData.amount);
                        if (item != null)
                        {
                            slot.SetItem(item);
                        }
                    }
                }

                inventorySlots.Add(slot);
            }

            inventoryProxy._inventory.ReplaceSlots(inventorySlots);
            RefreshAllSlots();
        }

        public SerializableInventorySlot[] Serialize() => inventoryProxy.GetSerializable();

        private void OnInventoryStateChanged(object sender)
        {
            RefreshAllSlots();
        }
        private void RefreshAllSlots()
        {
            var slots = inventoryProxy.Slots;
            int count = Mathf.Min(slots.Count, uiSlots.Length);
            for (int i = 0; i < count; i++)
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
