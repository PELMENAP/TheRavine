using System;
using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public class EventDrivenInventoryProxy : IDisposable
    {
        public event Action<object, IInventoryItem, int> OnInventoryItemAddedEvent;
        public event Action<object, Type, int> OnInventoryItemRemovedEvent;
        public event Action<object> OnInventoryStateChangedEvent;
        public event Action<object> OnInventoryStateChangedEventOnce;

        private readonly InventoryModel _inventory;

        public EventDrivenInventoryProxy(int slotCount)
        {
            _inventory = new InventoryModel(slotCount);
        }

        public EventDrivenInventoryProxy(List<InventorySlot> inventorySlots)
        {
            _inventory = new InventoryModel(inventorySlots);
        }

        public int capacity
        {
            get => _inventory.Capacity;
            set => _inventory.Capacity = value;
        }

        public bool isFull => _inventory.IsFull;

        public IInventoryItem GetItem(Type itemType) => _inventory.GetItem(itemType);
        public IInventoryItem[] GetAllItems() => _inventory.GetAllItems();
        public IInventoryItem[] GetAllItems(Type itemType) => _inventory.GetAllItems(itemType);
        public IInventoryItem[] GetEquippedItems() => _inventory.GetEquippedItems();
        public InventorySlot[] GetAllSlots() => _inventory.GetAllSlots();
        public int GetItemAmount(Type itemType) => _inventory.GetItemAmount(itemType);

        public bool TryToAdd(object sender, IInventoryItem item)
        {
            var initialAmount = item.state.amount;
            var success = _inventory.TryToAdd(item);

            if (success)
            {
                var addedAmount = initialAmount - item.state.amount;
                OnInventoryItemAddedEvent?.Invoke(sender, item, addedAmount);
                OnInventoryStateChangedEvent?.Invoke(sender);
                OnInventoryStateChangedEventOnce?.Invoke(sender);
            }

            return success;
        }

        public bool TryToAddSlot(object sender, InventorySlot slot, IInventoryItem item)
        {
            var initialAmount = item.state.amount;
            var success = _inventory.TryToAddSlot(slot, item);

            if (success)
            {
                var addedAmount = initialAmount - item.state.amount;
                OnInventoryItemAddedEvent?.Invoke(sender, item, addedAmount);
                OnInventoryStateChangedEvent?.Invoke(sender);
                OnInventoryStateChangedEventOnce?.Invoke(sender);
            }

            return success;
        }

        public void TransitFromSlotToSlot(object sender, InventorySlot fromSlot, InventorySlot toSlot)
        {
            _inventory.TransitFromSlotToSlot(fromSlot, toSlot);

            OnInventoryStateChangedEvent?.Invoke(sender);
            OnInventoryStateChangedEventOnce?.Invoke(sender);
        }

        public bool Remove(object sender, Type itemType, int amount = 1)
        {
            var success = _inventory.Remove(itemType, amount);

            if (success)
            {
                OnInventoryItemRemovedEvent?.Invoke(sender, itemType, amount);
                OnInventoryStateChangedEvent?.Invoke(sender);
                OnInventoryStateChangedEventOnce?.Invoke(sender);
            }

            return success;
        }
        public bool HasItem(Type itemType) => _inventory.HasItem(itemType);
        public bool HasItem(Type itemType, out IInventoryItem item) => _inventory.HasItem(itemType, out item);

        public void ClearSubscriptions()
        {
            OnInventoryItemAddedEvent = null;
            OnInventoryItemRemovedEvent = null;
            OnInventoryStateChangedEvent = null;
            OnInventoryStateChangedEventOnce = null;
        }

        public void Dispose()
        {
            ClearSubscriptions();
        }

        public SerializableInventorySlot[] GetSerializable() => _inventory.GetSerializable();
    }
}