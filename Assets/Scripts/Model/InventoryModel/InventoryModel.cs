using System;
using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public class InventoryModel
    {
        public int Capacity { get; set; }
        private int storageCapacity;
        private int occupiedStorage;
        public bool IsFull => occupiedStorage >= storageCapacity;
        private SerializableInventorySlot[] _serializableCache;

        private readonly List<InventorySlot> _slots;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        public InventoryModel(int capacity)
        {
            this.Capacity = capacity;
            _slots = new List<InventorySlot>(capacity);
            _serializableCache = new SerializableInventorySlot[capacity];

            for (int i = 0; i < capacity; i++)
            {
                _slots.Add(new InventorySlot());
                _serializableCache[i] = new SerializableInventorySlot();
            }
            storageCapacity = capacity;
        }

        public InventoryModel(SlotKind[] kinds)
        {
            int capacity = kinds.Length;
            this.Capacity = capacity;
            _slots = new List<InventorySlot>(capacity);
            _serializableCache = new SerializableInventorySlot[capacity];

            for (int i = 0; i < capacity; i++)
            {
                _slots.Add(new InventorySlot(kinds[i]));
                _serializableCache[i] = new SerializableInventorySlot();
                if (kinds[i].IsStorage()) storageCapacity++;
            }
        }

        private static bool CanPlace(IInventoryItemInfo info, SlotKind kind)
        {
            if (kind == SlotKind.CraftResult) return false;
            return kind.IsStorage() || info.Accepts(kind);
        }

        private void SetItem(InventorySlot slot, IInventoryItem item)
        {
            if (slot.isEmpty && slot.kind.IsStorage()) occupiedStorage++;
            slot.SetItem(item);
        }

        private void ClearSlot(InventorySlot slot)
        {
            if (!slot.isEmpty)
            {
                slot.Clear();
                if (slot.kind.IsStorage()) occupiedStorage--;
            }
        }

        public void ReplaceSlots(List<InventorySlot> newSlots)
        {
            _slots.Clear();
            _slots.AddRange(newSlots);

            if (_serializableCache.Length != newSlots.Count)
            {
                _serializableCache = new SerializableInventorySlot[newSlots.Count];
                for (int i = 0; i < newSlots.Count; i++)
                    _serializableCache[i] = new SerializableInventorySlot();
            }

            Capacity = newSlots.Count;
            storageCapacity = 0;
            occupiedStorage = 0;
            for (int i = 0; i < newSlots.Count; i++)
            {
                var slot = newSlots[i];
                if (!slot.kind.IsStorage()) continue;
                storageCapacity++;
                if (!slot.isEmpty) occupiedStorage++;
            }
        }

        public IInventoryItem GetItem(Type itemType)
        {
            if (itemType == null) return null;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].itemType == itemType) return _slots[i].item;
            return null;
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
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.isEmpty && slot.itemType == itemType) amount += slot.amount;
            }
            return amount;
        }

        private static bool IsSame(InventorySlot slot, Type type, IInventoryItemInfo info)
        {
            if (slot.isEmpty) return false;
            return type != null ? slot.itemType == type : string.Equals(slot.item.info.id, info.id);
        }

        private int FreeRoomCore(Type type, IInventoryItemInfo info, InventorySlot skip)
        {
            int max = info.maxItemsInInventorySlot;
            int room = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == skip || !slot.kind.IsStorage()) continue;
                if (slot.isEmpty) room += max > 0 ? max : 0;
                else if (!slot.isFull && IsSame(slot, type, info)) room += slot.capacity - slot.amount;
            }
            return room;
        }

        public int FreeRoomFor(IInventoryItemInfo info)
        {
            if (info == null) return 0;
            return FreeRoomCore(null, info, null);
        }

        public bool CanFit(IInventoryItemInfo info, int amount)
        {
            if (info == null) return false;
            if (amount <= 0) return true;
            return FreeRoomCore(null, info, null) >= amount;
        }

        private void AddToStorage(IInventoryItem item, int amount)
        {
            int left = amount;
            var type = item.type;
            for (int i = 0; i < _slots.Count && left > 0; i++)
            {
                var slot = _slots[i];
                if (!slot.kind.IsStorage() || slot.isEmpty || slot.isFull || slot.itemType != type) continue;
                int take = Math.Min(left, slot.capacity - slot.amount);
                slot.item.state.amount += take;
                left -= take;
            }
            int max = item.info.maxItemsInInventorySlot;
            for (int i = 0; i < _slots.Count && left > 0; i++)
            {
                var slot = _slots[i];
                if (!slot.kind.IsStorage() || !slot.isEmpty) continue;
                int take = Math.Min(left, max);
                var cloned = item.Clone();
                cloned.state.amount = take;
                SetItem(slot, cloned);
                left -= take;
            }
        }

        public bool TryToAdd(IInventoryItem item)
        {
            int amount = item.state.amount;
            if (amount <= 0) return false;
            if (FreeRoomCore(item.type, item.info, null) < amount) return false;
            AddToStorage(item, amount);
            return true;
        }

        public bool TryToAddSlot(InventorySlot slot, IInventoryItem item) => TryToAddSlotCore(slot, item, false);

        public bool TryProduceToSlot(InventorySlot slot, IInventoryItem item) => TryToAddSlotCore(slot, item, true);

        private bool TryToAddSlotCore(InventorySlot slot, IInventoryItem item, bool produce)
        {
            int amount = item.state.amount;
            if (amount <= 0) return false;
            if (produce)
            {
                if (slot.kind != SlotKind.CraftResult) return false;
            }
            else if (!CanPlace(item.info, slot.kind)) return false;

            int slotRoom;
            if (slot.isEmpty) slotRoom = item.info.maxItemsInInventorySlot;
            else if (slot.itemType == item.type) slotRoom = slot.capacity - slot.amount;
            else return false;
            if (slotRoom <= 0) return false;

            int put = Math.Min(slotRoom, amount);
            int rest = amount - put;
            if (rest > 0 && FreeRoomCore(item.type, item.info, slot) < rest) return false;

            if (slot.isEmpty)
            {
                var clonedItem = item.Clone();
                clonedItem.state.amount = put;
                SetItem(slot, clonedItem);
            }
            else
            {
                slot.item.state.amount += put;
            }

            if (rest > 0) AddToStorage(item, rest);
            return true;
        }

        public bool TransitFromSlotToSlot(InventorySlot fromSlot, InventorySlot toSlot)
        {
            if (fromSlot == toSlot || fromSlot.isEmpty) return false;
            if (!CanPlace(fromSlot.item.info, toSlot.kind)) return false;

            if (toSlot.isEmpty || toSlot.itemType == fromSlot.itemType)
            {
                var currentToAmount = toSlot.isEmpty ? 0 : toSlot.amount;
                var room = fromSlot.capacity - currentToAmount;
                if (room <= 0) return false;
                var amountToMove = Math.Min(fromSlot.amount, room);

                if (toSlot.isEmpty)
                {
                    var clonedItem = fromSlot.item.Clone();
                    clonedItem.state.amount = amountToMove;
                    SetItem(toSlot, clonedItem);
                }
                else
                {
                    toSlot.item.state.amount += amountToMove;
                }

                if (amountToMove == fromSlot.amount)
                    ClearSlot(fromSlot);
                else
                    fromSlot.item.state.amount -= amountToMove;
                return true;
            }

            if (!CanPlace(toSlot.item.info, fromSlot.kind)) return false;
            fromSlot.SwapItems(toSlot);
            return true;
        }

        public bool ConsumeFromSlot(InventorySlot slot, int amount)
        {
            if (slot == null || slot.isEmpty || amount <= 0 || amount > slot.amount) return false;
            slot.item.state.amount -= amount;
            if (slot.amount <= 0) ClearSlot(slot);
            return true;
        }

        public bool TrySplit(InventorySlot from, int amount, out InventorySlot to)
        {
            to = null;
            if (from == null || from.isEmpty || !from.kind.IsStorage()) return false;
            if (amount <= 0 || amount >= from.amount) return false;

            InventorySlot target = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot != from && slot.isEmpty && slot.kind.IsStorage())
                {
                    target = slot;
                    break;
                }
            }
            if (target == null) return false;

            var cloned = from.item.Clone();
            cloned.state.amount = amount;
            SetItem(target, cloned);
            from.item.state.amount -= amount;
            to = target;
            return true;
        }

        public bool TryUnequip(InventorySlot from)
        {
            if (from == null || from.isEmpty || !from.kind.IsEquipment()) return false;
            if (!TryToAdd(from.item)) return false;
            ClearSlot(from);
            return true;
        }

        public bool Remove(Type itemType, int amount = 1)
        {
            if (amount <= 0 || itemType == null) return false;
            if (GetItemAmount(itemType) < amount) return false;

            var left = amount;
            for (int i = _slots.Count - 1; i >= 0 && left > 0; i--)
            {
                var slot = _slots[i];
                if (slot.isEmpty || slot.itemType != itemType) continue;
                var take = Math.Min(slot.amount, left);
                ConsumeFromSlot(slot, take);
                left -= take;
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
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.isEmpty)
                    _serializableCache[i].SetEmpty();
                else
                    _serializableCache[i].Set(slot.item.info.id, slot.item.state.amount);
            }
            return _serializableCache;
        }
    }
}
