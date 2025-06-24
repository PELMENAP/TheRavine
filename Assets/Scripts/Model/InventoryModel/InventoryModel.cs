using System;
using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public class InventoryModel : IInventory
    {
        public int capacity { get; set; }
        public bool isFull => occupiedSlotsCount == capacity;

        private readonly IInventorySlot[] slots;
        private readonly Dictionary<Type, List<int>> typeToSlotIndices;
        private readonly Queue<int> emptySlotIndices;
        private int occupiedSlotsCount;

        public InventoryModel(int capacity)
        {
            this.capacity = capacity;
            slots = new IInventorySlot[capacity];
            typeToSlotIndices = new Dictionary<Type, List<int>>();
            emptySlotIndices = new Queue<int>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                slots[i] = new InventorySlot();
                emptySlotIndices.Enqueue(i);
            }

            occupiedSlotsCount = 0;
        }

        public IInventoryItem GetItem(Type itemType)
        {
            if (!typeToSlotIndices.TryGetValue(itemType, out var indices) || indices.Count == 0)
                return null;

            return slots[indices[0]].item;
        }

        public IInventoryItem[] GetAllItems()
        {
            var items = new IInventoryItem[occupiedSlotsCount];
            int index = 0;

            for (int i = 0; i < capacity; i++)
            {
                if (!slots[i].isEmpty)
                    items[index++] = slots[i].item;
            }

            return items;
        }
        public IInventoryItem[] GetAllItems(Type itemType)
        {
            if (!typeToSlotIndices.TryGetValue(itemType, out var indices))
                return new IInventoryItem[0];

            var items = new IInventoryItem[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                items[i] = slots[indices[i]].item;
            }

            return items;
        }

        public IInventoryItem[] GetEquippedItems()
        {
            var equippedItems = new List<IInventoryItem>();

            for (int i = 0; i < capacity; i++)
            {
                if (!slots[i].isEmpty && slots[i].item.state.isEquipped)
                    equippedItems.Add(slots[i].item);
            }

            return equippedItems.ToArray();
        }

        public int GetItemAmount(Type itemType)
        {
            if (!typeToSlotIndices.TryGetValue(itemType, out var indices))
                return 0;

            int totalAmount = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                totalAmount += slots[indices[i]].amount;
            }

            return totalAmount;
        }

        public bool TryToAdd(object sender, IInventoryItem item)
        {
            int remaining = item.state.amount;

            if (typeToSlotIndices.TryGetValue(item.type, out var indices))
            {
                for (int i = 0; i < indices.Count && remaining > 0; i++)
                {
                    var slotIndex = indices[i];
                    var slot = slots[slotIndex];
                    if (slot.isFull) continue;

                    int availableSpace = slot.item.info.maxItemsInInventorySlot - slot.amount;
                    int amountToAdd = Math.Min(remaining, availableSpace);
                    
                    slot.item.state.amount += amountToAdd;
                    remaining -= amountToAdd;
                }
            }

            while (remaining > 0 && emptySlotIndices.Count > 0)
            {
                int slotIndex = emptySlotIndices.Dequeue();
                var slot = slots[slotIndex];
                int maxInSlot = item.info.maxItemsInInventorySlot;
                int amountToAdd = Math.Min(remaining, maxInSlot);

                var clone = item.Clone();
                clone.state.amount = amountToAdd;
                slot.SetItem(clone);
                UpdateIndicesOnAdd(slotIndex, item.type);
                remaining -= amountToAdd;
            }

            item.state.amount = remaining;
            return remaining == 0;
        }

        public bool Remove(object sender, Type itemType, int amount = 1)
        {
            if (!typeToSlotIndices.TryGetValue(itemType, out var indices) || indices.Count == 0)
                return false;

            int amountToRemove = amount;
            var slotsToRemove = new List<int>();

            for (int i = indices.Count - 1; i >= 0 && amountToRemove > 0; i--)
            {
                var slotIndex = indices[i];
                var slot = slots[slotIndex];
                var availableAmount = slot.amount;
                var removeFromSlot = Math.Min(amountToRemove, availableAmount);

                slot.item.state.amount -= removeFromSlot;
                amountToRemove -= removeFromSlot;

                if (slot.amount <= 0)
                {
                    slotsToRemove.Add(slotIndex);
                }
            }

            foreach (var slotIndex in slotsToRemove)
            {
                slots[slotIndex].Clear();
                UpdateIndicesOnRemove(slotIndex, itemType);
            }

            return amountToRemove == 0;
        }

        public bool HasItem(Type itemType, out IInventoryItem item)
        {
            item = GetItem(itemType);
            return item != null;
        }

        public bool HasItem(Type itemType)
        {
            return typeToSlotIndices.ContainsKey(itemType) && typeToSlotIndices[itemType].Count > 0;
        }

        private void UpdateIndicesOnAdd(int slotIndex, Type itemType)
        {
            if (!typeToSlotIndices.ContainsKey(itemType))
                typeToSlotIndices[itemType] = new List<int>();

            typeToSlotIndices[itemType].Add(slotIndex);
            occupiedSlotsCount++;
        }

        private void UpdateIndicesOnRemove(int slotIndex, Type itemType)
        {
            if (typeToSlotIndices.TryGetValue(itemType, out var indices))
            {
                indices.Remove(slotIndex);
                if (indices.Count == 0)
                    typeToSlotIndices.Remove(itemType);
            }

            emptySlotIndices.Enqueue(slotIndex);
            occupiedSlotsCount--;
        }
        public IInventorySlot[] GetAllSlots()
        {
            return slots;
        }

        public IInventorySlot[] GetAllSlots(Type itemType)
        {
            if (!typeToSlotIndices.TryGetValue(itemType, out var indices))
                return new IInventorySlot[0];

            var result = new IInventorySlot[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                result[i] = this.slots[indices[i]];
            }
            return result;
        }

        public void TransitFromSlotToSlot(object sender, int fromSlotIndex, int toSlotIndex)
        {
            if (fromSlotIndex == toSlotIndex || 
                fromSlotIndex < 0 || fromSlotIndex >= capacity ||
                toSlotIndex < 0 || toSlotIndex >= capacity)
            {
                return;
            }

            var fromSlot = slots[fromSlotIndex];
            var toSlot = slots[toSlotIndex];

            if (fromSlot.isEmpty || 
                toSlot.isFull || 
                (!toSlot.isEmpty && fromSlot.itemType != toSlot.itemType))
            {
                return;
            }

            var itemInfo = fromSlot.item.info;
            var maxCapacity = itemInfo.maxItemsInInventorySlot;
            var availableSpace = toSlot.isEmpty 
                ? maxCapacity 
                : maxCapacity - toSlot.amount;
            
            var amountToMove = Math.Min(fromSlot.amount, availableSpace);

            if (amountToMove <= 0)
            {
                return;
            }

            if (toSlot.isEmpty)
            {
                var clonedItem = fromSlot.item.Clone();
                clonedItem.state.amount = amountToMove;
                toSlot.SetItem(clonedItem);
                UpdateIndicesOnAdd(toSlotIndex, fromSlot.itemType);
            }
            else
            {
                toSlot.item.state.amount += amountToMove;
            }

            fromSlot.item.state.amount -= amountToMove;

            if (fromSlot.amount <= 0)
            {
                fromSlot.Clear();
                UpdateIndicesOnRemove(fromSlotIndex, fromSlot.itemType);
            }
        }
    }
}