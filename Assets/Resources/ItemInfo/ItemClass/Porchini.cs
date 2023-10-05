using System;
using UnityEngine;

public class Porchini : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "Всегда белый\r\n- тонищирующее действие\r\n- предпочитает мох и лишайник\r\n- самый главный гриб\r\n80 ккал";
    
    public Porchini(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedPorchini = new Porchini(info);
        clonedPorchini.state.amount = state.amount;
        return clonedPorchini;
    }
}
