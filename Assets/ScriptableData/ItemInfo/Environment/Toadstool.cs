namespace TheRavine.Inventory
{
    public class Toadstool : ItemClass
    {
        public string description = "Маленькая зловредная\r\n- неприятный сладковатый запах\r\n- предпочитает плодородные почвы\r\n- токсины не убрать\r\n30 ккал + смертельное отравление";
        public Toadstool(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Toadstool(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}
