using System;
using UnityEngine;

public class Boletus : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "Оранжевая шапочка\r\n- вполне сьедобен\r\n- предпочитает лиственные леса\r\n- цвет осенних листьев\r\n60 ккал";
    public Boletus(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedBoletus = new Boletus(info);
        clonedBoletus.state.amount = state.amount;
        return clonedBoletus;
    }
}
