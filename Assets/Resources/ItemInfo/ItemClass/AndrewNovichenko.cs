using System;

public class AndrewNovichenko : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "норм \r\nкак ты это скрафтил? \r\nпоёдем калимбасика дадим \r\nили на зуб";
    
    public AndrewNovichenko(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedPorchini = new AndrewNovichenko(info);
        clonedPorchini.state.amount = state.amount;
        return clonedPorchini;
    }
}
