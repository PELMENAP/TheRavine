namespace TheRavine.Inventory
{
    public class Politics : ItemClass
    {
        public string description = "Государство \r\rобщественные институты и отдельные личности реализуют свои интересы при помощи власти";

        public Politics(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Politics(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}