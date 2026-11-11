namespace TheRavine.Inventory
{
    public class AspenBolete : ItemClass
    {
        public string description = "Оранжевая шапочка\r\n- вполне сьедобен\r\n- предпочитает лиственные леса\r\n- цвет осенних листьев\r\n60 ккал";
        public AspenBolete(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new AspenBolete(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}