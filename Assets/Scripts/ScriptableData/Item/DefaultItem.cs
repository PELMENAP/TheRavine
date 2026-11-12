namespace TheRavine.Inventory
{
    public sealed class DefaultItem : ItemClass
    {
        public DefaultItem(IInventoryItemInfo info) : base(info)
        {
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new DefaultItem(info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}
