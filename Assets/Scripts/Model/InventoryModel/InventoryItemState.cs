using System;

[Serializable]
public class InventoryItemState : IInventoryItemState
{
    public int itemAmount;
    public bool isItemEquipped;
    public int amount { get => itemAmount; set => itemAmount = value; }
    public bool isEquipped { get => isItemEquipped; set => isItemEquipped = value; }

    public InventoryItemState()
    {
        itemAmount = 0;
        isItemEquipped = false;
    }
}
