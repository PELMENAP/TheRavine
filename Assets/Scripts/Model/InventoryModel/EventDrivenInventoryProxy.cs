using System;
using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public class EventDrivenInventoryProxy : IDisposable
    {
        public event Action<object, IInventoryItem, int> OnInventoryItemAddedEvent;
        public event Action<object, string, int> OnInventoryItemRemovedEvent;
        public event Action<object> OnInventoryStateChangedEvent;
        public event Action<object> OnInventoryStateChangedEventOnce;

        public readonly InventoryModel _inventory;

        public EventDrivenInventoryProxy(int slotCount)
        {
            _inventory = new InventoryModel(slotCount);
        }

        public EventDrivenInventoryProxy(SlotKind[] kinds)
        {
            _inventory = new InventoryModel(kinds);
        }

        public int capacity
        {
            get => _inventory.Capacity;
            set => _inventory.Capacity = value;
        }

        public bool isFull => _inventory.IsFull;
        public IReadOnlyList<InventorySlot> Slots => _inventory.Slots;

        public IInventoryItem GetItem(string itemId) => _inventory.GetItem(itemId);
        public IInventoryItem[] GetAllItems() => _inventory.GetAllItems();
        public IInventoryItem[] GetAllItems(string itemId) => _inventory.GetAllItems(itemId);
        public IInventoryItem[] GetEquippedItems() => _inventory.GetEquippedItems();
        public InventorySlot[] GetAllSlots() => _inventory.GetAllSlots();
        public int GetItemAmount(string itemId) => _inventory.GetItemAmount(itemId);
        public bool CanFit(IInventoryItemInfo info, int amount) => _inventory.CanFit(info, amount);
        public int FreeRoomFor(IInventoryItemInfo info) => _inventory.FreeRoomFor(info);

        public void NotifyStateChanged(object sender)
        {
            OnInventoryStateChangedEvent?.Invoke(sender);
            OnInventoryStateChangedEventOnce?.Invoke(sender);
        }

        public bool TryToAdd(object sender, IInventoryItem item)
        {
            var amount = item.state.amount;
            var success = _inventory.TryToAdd(item);

            if (success)
            {
                OnInventoryItemAddedEvent?.Invoke(sender, item, amount);
                NotifyStateChanged(sender);
            }

            return success;
        }

        public bool TryToAddSlot(object sender, InventorySlot slot, IInventoryItem item)
        {
            var amount = item.state.amount;
            var success = _inventory.TryToAddSlot(slot, item);

            if (success)
            {
                OnInventoryItemAddedEvent?.Invoke(sender, item, amount);
                NotifyStateChanged(sender);
            }

            return success;
        }

        public bool TryProduceToSlot(object sender, InventorySlot slot, IInventoryItem item)
        {
            var amount = item.state.amount;
            var success = _inventory.TryProduceToSlot(slot, item);

            if (success)
            {
                OnInventoryItemAddedEvent?.Invoke(sender, item, amount);
                NotifyStateChanged(sender);
            }

            return success;
        }

        public bool TransitFromSlotToSlot(object sender, InventorySlot fromSlot, InventorySlot toSlot)
        {
            var changed = _inventory.TransitFromSlotToSlot(fromSlot, toSlot);
            if (changed) NotifyStateChanged(sender);
            return changed;
        }

        public bool ConsumeFromSlot(object sender, InventorySlot slot, int amount, bool notify = true)
        {
            var id = slot?.itemId;
            var success = _inventory.ConsumeFromSlot(slot, amount);

            if (success)
            {
                OnInventoryItemRemovedEvent?.Invoke(sender, id, amount);
                if (notify) NotifyStateChanged(sender);
            }

            return success;
        }

        public bool TrySplit(object sender, InventorySlot from, int amount, out InventorySlot to)
        {
            var success = _inventory.TrySplit(from, amount, out to);
            if (success) NotifyStateChanged(sender);
            return success;
        }

        public bool TryUnequip(object sender, InventorySlot from)
        {
            var success = _inventory.TryUnequip(from);
            if (success) NotifyStateChanged(sender);
            return success;
        }

        public bool Remove(object sender, string itemId, int amount = 1)
        {
            var success = _inventory.Remove(itemId, amount);

            if (success)
            {
                OnInventoryItemRemovedEvent?.Invoke(sender, itemId, amount);
                NotifyStateChanged(sender);
            }

            return success;
        }
        public bool HasItem(string itemId) => _inventory.HasItem(itemId);
        public bool HasItem(string itemId, out IInventoryItem item) => _inventory.HasItem(itemId, out item);

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
