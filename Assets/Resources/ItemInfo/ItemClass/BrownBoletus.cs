using System;
using UnityEngine;
public class BrownBoletus : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "Невзрачная нога\r\n- хоть как можно есть\r\n- частый экземпляр\r\n- главный любитель берёз\r\n50 ккал";
    public BrownBoletus(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedBrownBoletus = new BrownBoletus(info);
        clonedBrownBoletus.state.amount = state.amount;
        return clonedBrownBoletus;
    }
}
