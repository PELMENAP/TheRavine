using System;
using UnityEngine;

public class Toadstool : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "Маленькая зловредная\r\n- неприятный сладковатый запах\r\n- предпочитает плодородные почвы\r\n- токсины не убрать\r\n30 ккал + отравление";
    public Toadstool(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedToadstool = new Toadstool(info);
        clonedToadstool.state.amount = state.amount;
        return clonedToadstool;
    }
}
