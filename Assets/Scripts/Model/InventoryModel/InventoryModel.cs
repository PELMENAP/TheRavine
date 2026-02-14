using System;
using System.Collections.Generic;
using ZLinq;

namespace TheRavine.Inventory
{
    public class InventoryModel
    {
        public int capacity { get; set; }
        public bool isFull => _slots.AsValueEnumerable().All(slot => slot.isFull);

        private readonly List<InventorySlot> _slots;
        public InventoryModel(int capacity)
        {
            this.capacity = capacity;
            _slots = new List<InventorySlot>(capacity);
            for (int i = 0; i < capacity; i++) _slots.Add(new InventorySlot());
        }

        public InventoryModel(List<InventorySlot> inventorySlots)
        {
            this.capacity = inventorySlots.Count;
            _slots = inventorySlots;
        }
        public IInventoryItem GetItem(Type itemType)
        {
            return _slots.Find(slot => slot.itemType == itemType).item;
        }
        public IInventoryItem[] GetAllItems()
        {
            var allItems = new List<IInventoryItem>();
            foreach (var slot in _slots)
                if (!slot.isEmpty)
                    allItems.Add(slot.item);
            return allItems.ToArray();
        }
        public IInventoryItem[] GetAllItems(Type itemType)
        {
            var allItemsOfType = new List<IInventoryItem>();
            var slotsOfType = _slots.FindAll(slot => !slot.isEmpty && slot.itemType == itemType);
            foreach (var slot in slotsOfType)
                allItemsOfType.Add(slot.item);
            return allItemsOfType.ToArray();
        }
        public IInventoryItem[] GetEquippedItems()
        {
            var requiredSlots = _slots.FindAll(slot => !slot.isEmpty && slot.item.state.isEquipped);
            var equippedItems = new List<IInventoryItem>();
            foreach (var slot in requiredSlots)
                equippedItems.Add(slot.item);
            return equippedItems.ToArray();

        }
        public int GetItemAmount(Type itemType)
        {
            var amount = 0;
            var allItemSlots = _slots.FindAll(slot => !slot.isEmpty && slot.itemType == itemType);
            foreach (var itemSlot in allItemSlots)
                amount += itemSlot.amount;
            return amount;
        }
        public bool TryToAdd(object sender, IInventoryItem item)
        {
            var slotWithSameItemButNotEmpty = _slots.Find(slot => !slot.isEmpty && slot.itemType == item.type && !slot.isFull);
            if (slotWithSameItemButNotEmpty != null)
                return TryToAddSlot(sender, slotWithSameItemButNotEmpty, item);
            var emptySlot = _slots.Find(slot => slot.isEmpty);
            if (emptySlot != null)
                return TryToAddSlot(sender, emptySlot, item);
            return false;
        }

        public bool TryToAddSlot(object sender, InventorySlot slot, IInventoryItem item)
        {
            var fits = slot.amount + item.state.amount <= item.info.maxItemsInInventorySlot;
            var amountToAdd = fits ? item.state.amount : item.info.maxItemsInInventorySlot - slot.amount;
            var amountLeft = item.state.amount - amountToAdd;
            if (slot.isEmpty)
            {
                var clonedItem = item.Clone();
                clonedItem.state.amount = amountToAdd;
                slot.SetItem(clonedItem);
            }
            else
                slot.item.state.amount += amountToAdd;
            if (amountLeft <= 0)
                return true;
            item.state.amount = amountLeft;
            return TryToAdd(sender, item);
        }

        public void TransitFromSlotToSlot(object sender, InventorySlot fromSlot, InventorySlot toSlot)
        {
            if (fromSlot == toSlot || fromSlot.isEmpty || toSlot.isFull || (!toSlot.isEmpty && fromSlot.itemType != toSlot.itemType))
                return;
            var slotCapacity = fromSlot.capacity;
            var fits = fromSlot.amount + toSlot.amount <= slotCapacity;
            var amountToAdd = fits ? fromSlot.amount : slotCapacity - toSlot.amount;
            var amountLeft = fromSlot.amount - amountToAdd;
            if (toSlot.isEmpty)
            {
                toSlot.SetItem(fromSlot.item);
                fromSlot.Clear();
            }

            toSlot.item.state.amount += amountToAdd;
            if (fits)
                fromSlot.Clear();
            else
                fromSlot.item.state.amount = amountLeft;
        }
        public bool Remove(object sender, Type itemType, int amount = 1)
        {
            var slotsWithItem = GetAllSlots(itemType);
            if (slotsWithItem.Length == 0)
                return false;
            var amountToRemove = amount;
            var count = slotsWithItem.Length;
            for (int i = count - 1; i >= 0; i--)
            {
                var slot = slotsWithItem[i];
                if (slot.amount >= amountToRemove)
                {
                    slot.item.state.amount -= amountToRemove;
                    if (slot.amount <= 0)
                        slot.Clear();
                    break;
                }
                amountToRemove -= slot.amount;
                slot.Clear();
            }
            return true;
        }
        public bool HasItem(Type itemType, out IInventoryItem item)
        {
            item = GetItem(itemType);
            return item != null;
        }
        public bool HasItem(Type itemType)
        {
            for(int i = 0; i < _slots.Count; i++) if(_slots[i].itemType == itemType) return true;
            return false;
        }

        public InventorySlot[] GetAllSlots(Type itemType)
        {
            return _slots.FindAll(slot => !slot.isEmpty && slot.itemType == itemType).ToArray();
        }

        public InventorySlot[] GetAllSlots()
        {
            return _slots.ToArray();
        }

        public SerializableInventorySlot[] GetSerializable()
        {
            SerializableInventorySlot[] data = new SerializableInventorySlot[capacity];
            for(int i = 0; i < capacity; i++)
            {
                if(_slots[i].isEmpty) data[i] = new SerializableInventorySlot("empty", 0);
                var item = _slots[i].item;
                if(item == null) continue;
                data[i] = new SerializableInventorySlot(item.info.id, item.state.amount);
            }
            return data;
        }
    }
}