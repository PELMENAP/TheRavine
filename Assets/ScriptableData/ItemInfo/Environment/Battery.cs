public class Battery : ItemClass
{
    public string description = "Несколько соединенных \r\nмежду собой гальванических элементов";
    
    public Battery(IInventoryItemInfo info) : base(info)
    {
        this.info.description = description;
    }

    public override IInventoryItem Clone()
    {
        var clonedItem = new Battery(this.info);
        clonedItem.CopyFrom(this);
        return clonedItem;
    }
}
