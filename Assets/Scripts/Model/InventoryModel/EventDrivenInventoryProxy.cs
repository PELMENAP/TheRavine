using System;

namespace TheRavine.Inventory
{
    public class EventDrivenInventoryProxy : IInventory, IDisposable
    {
        public event Action<object, IInventoryItem, int> OnInventoryItemAddedEvent;
        public event Action<object, Type, int> OnInventoryItemRemovedEvent;
        public event Action<object> OnInventoryStateChangedEvent;
        public event Action<object> OnInventoryStateChangedEventOnce;

        private readonly IInventory _inventory;

        public EventDrivenInventoryProxy(IInventory inventory)
        {
            _inventory = inventory;
        }

        public int capacity
        {
            get => _inventory.capacity;
            set => _inventory.capacity = value;
        }

        public bool isFull => _inventory.isFull;

        public IInventoryItem GetItem(Type itemType) => _inventory.GetItem(itemType);
        public IInventoryItem[] GetAllItems() => _inventory.GetAllItems();
        public IInventoryItem[] GetAllItems(Type itemType) => _inventory.GetAllItems(itemType);
        public IInventoryItem[] GetEquippedItems() => _inventory.GetEquippedItems();
        public IInventorySlot[] GetAllSlots() => _inventory.GetAllSlots();
        public int GetItemAmount(Type itemType) => _inventory.GetItemAmount(itemType);

        public bool TryToAdd(object sender, IInventoryItem item)
        {
            var initialAmount = item.state.amount;
            var success = _inventory.TryToAdd(sender, item);

            if (success)
            {
                var addedAmount = initialAmount - item.state.amount;
                OnInventoryItemAddedEvent?.Invoke(sender, item, addedAmount);
                OnInventoryStateChangedEvent?.Invoke(sender);
                OnInventoryStateChangedEventOnce?.Invoke(sender);
            }

            return success;
        }

        public bool TryToAddSlot(object sender, IInventorySlot slot, IInventoryItem item)
        {
            var initialAmount = item.state.amount;
            var success = _inventory.TryToAddSlot(sender, slot, item);

            if (success)
            {
                var addedAmount = initialAmount - item.state.amount;
                OnInventoryItemAddedEvent?.Invoke(sender, item, addedAmount);
                OnInventoryStateChangedEvent?.Invoke(sender);
                OnInventoryStateChangedEventOnce?.Invoke(sender);
            }

            return success;
        }

        public bool Remove(object sender, Type itemType, int amount = 1)
        {
            var success = _inventory.Remove(sender, itemType, amount);

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
    }
}