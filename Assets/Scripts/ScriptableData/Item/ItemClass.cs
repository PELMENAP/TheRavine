using System;

namespace TheRavine.Inventory
{

    public abstract class ItemClass : IInventoryItem
    {
        public IInventoryItemInfo info { get; }
        public IInventoryItemState state { get; }
        public Type type => GetType();

        public ItemClass(IInventoryItemInfo info)
        {
            this.info = info;
            state = new InventoryItemState();
        }

        public abstract IInventoryItem Clone();

        protected void CopyFrom(ItemClass source)
        {
            this.state.amount = source.state.amount;
        }
    }
}