namespace TheRavine.Inventory
{
    public class Boletus : ItemClass
    {
        public string description = "Оранжевая шапочка\r\n- вполне сьедобен\r\n- предпочитает лиственные леса\r\n- цвет осенних листьев\r\n60 ккал";
        public Boletus(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Boletus(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}