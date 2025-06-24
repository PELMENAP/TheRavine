namespace TheRavine.Inventory
{
    public class AndrewNovichenko : ItemClass
    {
        public string description = "норм \r\nкак ты это скрафтил? \r\nпоёдем калимбасика дадим \r\nили на зуб";

        public AndrewNovichenko(IInventoryItemInfo info) : base(info)
        {
            this.info.description = description;
        }

        public override IInventoryItem Clone()
        {
            var clonedItem = new AndrewNovichenko(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }
    }
}