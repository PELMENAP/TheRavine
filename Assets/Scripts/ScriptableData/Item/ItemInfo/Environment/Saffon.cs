namespace TheRavine.Inventory
{
    public class Saffron : ItemClass
    {
        public string description = "Рыжий \r\n- интересно, почему он рыжий? \r\n- Засолил бы? \r\n30 ккал";
        public Saffron(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Saffron(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}
