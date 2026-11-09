using System.Collections.Generic;

namespace TheRavine.Inventory
{
    public sealed class PickUpReservation
    {
        private readonly Dictionary<IInventoryItemInfo, int> reserved = new();

        public int Reserved(IInventoryItemInfo info)
        {
            reserved.TryGetValue(info, out int current);
            return current;
        }

        public bool TryReserve(IInventoryItemInfo info, int amount, int freeRoom)
        {
            if (info == null || amount <= 0) return false;
            reserved.TryGetValue(info, out int current);
            if (current + amount > freeRoom) return false;
            reserved[info] = current + amount;
            return true;
        }

        public void Release(IInventoryItemInfo info, int amount)
        {
            if (info == null || !reserved.TryGetValue(info, out int current)) return;
            int left = current - amount;
            if (left > 0) reserved[info] = left;
            else reserved.Remove(info);
        }

        public void Clear()
        {
            reserved.Clear();
        }
    }
}
