public class BrownBoletus : ItemClass
{
    public string description = "Невзрачная нога\r\n- хоть как можно есть\r\n- частый экземпляр\r\n- главный любитель берёз\r\n50 ккал";
    public BrownBoletus(IInventoryItemInfo info) : base(info)
    {
        this.info.description = description;
    }

    public override IInventoryItem Clone()
    {
        var clonedItem = new BrownBoletus(this.info);
        clonedItem.CopyFrom(this);
        return clonedItem;
    }
}
