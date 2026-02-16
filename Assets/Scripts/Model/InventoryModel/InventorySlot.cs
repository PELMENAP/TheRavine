using System;

public class InventorySlot
{
    public bool isFull => !isEmpty && amount == capacity;
    private bool _cachedIsEmpty = true;
    public bool isEmpty => _cachedIsEmpty;

    public IInventoryItem item { get; private set; }
    public Type itemType => isEmpty ? null : item.type;
    public int amount => isEmpty ? 0 : item.state.amount;
    public int capacity { get; private set; }
    public void SetItem(IInventoryItem item)
    {
        if (!isEmpty) return;
        this.item = item;
        this.capacity = item.info.maxItemsInInventorySlot;
        _cachedIsEmpty = false;
    }
    
    public void Clear()
    {
        if (isEmpty) return;
        item.state.amount = 0;
        item = null;
        _cachedIsEmpty = true;
    }
}
