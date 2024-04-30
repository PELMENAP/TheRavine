using System;
using UnityEngine;

public class PlayerItem : IInventoryItem
{
    public IInventoryItemInfo info { get; }
    public IInventoryItemState state { get; }
    public Type type => GetType();
    public string _description = "что блять, это че за хуйня? \r\nкак ты это скрафтил? \r\nхарактер нормисный \r\nне женат";
    
    public PlayerItem(IInventoryItemInfo info)
    {
        this.info = info;
        this.info.description = _description;
        state = new InventoryItemState();
    }

    public IInventoryItem Clone()
    {
        var clonedPorchini = new PlayerItem(info);
        clonedPorchini.state.amount = state.amount;
        return clonedPorchini;
    }
}
