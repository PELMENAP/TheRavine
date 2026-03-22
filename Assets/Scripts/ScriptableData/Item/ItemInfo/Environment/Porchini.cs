namespace TheRavine.Inventory
{
    public class Porchini : ItemClass
    {
        public string description = "Всегда белый\r\n- тонищирующее действие\r\n- предпочитает мох и лишайник\r\n- самый главный гриб\r\n80 ккал";

        public Porchini(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new Porchini(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}