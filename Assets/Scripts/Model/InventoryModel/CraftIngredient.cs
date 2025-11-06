using System;
using UnityEngine;

[Serializable]
public struct CraftIngredient
{
    public InventoryItemInfo info;
    [Min(1)] public int amount;
}