namespace TheRavine.Inventory
{
    public class Jirinovskiy : ItemClass
    {
        public string description = "Это не Германия, это не Афганистан. \r\nДжордж, твоих солдат здесь порвут на части  \r\n250 тысяч отборных солдат Ирака!  \r\nОни все разнесут. ";

        public Jirinovskiy(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Jirinovskiy(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}