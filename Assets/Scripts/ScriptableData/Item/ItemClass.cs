using System;

using TheRavine.EntityControl;

namespace TheRavine.Inventory
{
    public struct UseContext
    {
        public AEntity Entity;
        public EventDrivenInventoryProxy Inventory;
    }

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

        public virtual bool IsUsable => false;

        public virtual bool TryUse(UseContext context) => false;

        public abstract IInventoryItem Clone();

        protected void CopyFrom(ItemClass source)
        {
            this.state.amount = source.state.amount;
        }
    }
}