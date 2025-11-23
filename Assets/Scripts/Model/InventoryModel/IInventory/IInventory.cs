using System;
public interface IInventory
{
    int capacity { get; set; }
    bool isFull { get; }
    IInventoryItem GetItem(Type itemType);
    IInventoryItem[] GetAllItems();
    IInventoryItem[] GetAllItems(Type itemType);
    IInventoryItem[] GetEquippedItems();
    IInventorySlot[] GetAllSlots();
    int GetItemAmount(Type itemType);

    bool TryToAdd(object sender, IInventoryItem item);

    bool Remove(object sender, Type itemType, int amount = 1);
    bool HasItem(Type itemType);
    bool HasItem(Type itemType, out IInventoryItem item);
}   