using System;
using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public class InventoryModel
    {
        public int Capacity { get; set; }
        private int occupiedSlots;
        public bool IsFull => occupiedSlots == Capacity;

        private readonly HashSet<int> _dirtySlots = new HashSet<int>();
        private SerializableInventorySlot[] _serializableCache;

        private void SetItem(InventorySlot slot, IInventoryItem item)
        {
            if (slot.isEmpty) occupiedSlots++;
            slot.SetItem(item);
            MarkSlotDirty(_slots.IndexOf(slot));
        }

        private void ClearSlot(InventorySlot slot)
        {
            if (!slot.isEmpty)
            {
                slot.Clear();
                occupiedSlots--;
            }
        }


        private readonly List<InventorySlot> _slots;
        public InventoryModel(int capacity)
        {
            this.Capacity = capacity;
            _slots = new List<InventorySlot>(capacity);
            for (int i = 0; i < capacity; i++) _slots.Add(new InventorySlot());
        }

        public InventoryModel(List<InventorySlot> inventorySlots)
        {
            this.Capacity = inventorySlots.Count;
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
        public bool TryToAdd(IInventoryItem item)
        {
            var slotWithSameItemButNotEmpty = _slots.Find(slot => !slot.isEmpty && slot.itemType == item.type && !slot.isFull);
            if (slotWithSameItemButNotEmpty != null)
                return TryToAddSlot(slotWithSameItemButNotEmpty, item);
            var emptySlot = _slots.Find(slot => slot.isEmpty);
            if (emptySlot != null)
                return TryToAddSlot(emptySlot, item);
            return false;
        }

        public bool TryToAddSlot(InventorySlot slot, IInventoryItem item)
        {
            var fits = slot.amount + item.state.amount <= item.info.maxItemsInInventorySlot;
            var amountToAdd = fits ? item.state.amount : item.info.maxItemsInInventorySlot - slot.amount;
            var amountLeft = item.state.amount - amountToAdd;
            if (slot.isEmpty)
            {
                var clonedItem = item.Clone();
                clonedItem.state.amount = amountToAdd;
                
                SetItem(slot, clonedItem);
            }
            else
                slot.item.state.amount += amountToAdd;
            if (amountLeft <= 0)
                return true;
            item.state.amount = amountLeft;
            return TryToAdd(item);
        }

        public void TransitFromSlotToSlot(InventorySlot fromSlot, InventorySlot toSlot)
        {
            if (fromSlot == toSlot || fromSlot.isEmpty) return;
            if (!toSlot.isEmpty && fromSlot.itemType != toSlot.itemType) return;
            
            var slotCapacity = fromSlot.capacity;
            var currentToAmount = toSlot.isEmpty ? 0 : toSlot.amount;
            var fits = fromSlot.amount + currentToAmount <= slotCapacity;
            var amountToAdd = fits ? fromSlot.amount : slotCapacity - currentToAmount;
            var amountLeft = fromSlot.amount - amountToAdd;
            
            if (toSlot.isEmpty)
            {
                var clonedItem = fromSlot.item.Clone();
                clonedItem.state.amount = amountToAdd;
                SetItem(toSlot, clonedItem);
            }
            else
            {
                toSlot.item.state.amount += amountToAdd;
            }
            
            if (fits)
                ClearSlot(fromSlot);
            else
                fromSlot.item.state.amount = amountLeft;
        }

        public bool Remove(Type itemType, int amount = 1)
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
                        ClearSlot(slot);
                    break;
                }
                amountToRemove -= slot.amount;
                ClearSlot(slot);
            }
            return amountToRemove <= 0;
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

        private void MarkSlotDirty(int slotIndex)
        {
            _dirtySlots.Add(slotIndex);
        }

        public SerializableInventorySlot[] GetSerializable()
        {
            if (_serializableCache == null || _serializableCache.Length != Capacity)
            {
                _serializableCache = new SerializableInventorySlot[Capacity];
                for (int i = 0; i < Capacity; i++)
                    UpdateSerializableSlot(i);
                _dirtySlots.Clear();
            }
            else if (_dirtySlots.Count > 0)
            {
                foreach (var index in _dirtySlots)
                    UpdateSerializableSlot(index);
                _dirtySlots.Clear();
            }
            
            return _serializableCache;
        }
        private void UpdateSerializableSlot(int index)
        {
            _serializableCache[index] = _slots[index].isEmpty 
                ? new SerializableInventorySlot { isEmpty = true, title = "", amount = 0 }
                : new SerializableInventorySlot 
                { 
                    isEmpty = false,
                    title = _slots[index].item.info.id, 
                    amount = _slots[index].item.state.amount 
                };
        }
    }
}