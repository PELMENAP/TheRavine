namespace TheRavine.Inventory
{
    public class Isa : ItemClass
    {
        public string description = "Добрый день! \r\nЧем могу помочь?";

        public Isa(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Isa(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}