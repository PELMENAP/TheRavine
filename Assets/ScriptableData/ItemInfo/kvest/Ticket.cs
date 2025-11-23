namespace TheRavine.Inventory
{
    public class Ticket : ItemClass
    {
        public string description = "Билет в один конец - домой \r\nномер 124115";

        public Ticket(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Ticket(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}