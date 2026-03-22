namespace TheRavine.Inventory
{
    public class Kirieshky : ItemClass
    {
        public string description = "Ржано-пшеничные сухарики \r\nсо вкусом сыра \r\n60 грамм";

        public Kirieshky(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Kirieshky(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}