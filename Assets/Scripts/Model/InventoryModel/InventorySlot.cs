using System;

public class InventorySlot
{
    public readonly SlotKind kind;
    public bool isFull => !isEmpty && amount == capacity;
    private bool _cachedIsEmpty = true;
    public bool isEmpty => _cachedIsEmpty;

    public IInventoryItem item { get; private set; }
    public Type itemType => isEmpty ? null : item.type;
    public int amount => isEmpty ? 0 : item.state.amount;
    public int capacity { get; private set; }

    public InventorySlot()
    {
        kind = SlotKind.Bag;
    }

    public InventorySlot(SlotKind kind)
    {
        this.kind = kind;
    }

    public void SetItem(IInventoryItem item)
    {
        if (!isEmpty) return;
        this.item = item;
        this.capacity = item.info.maxItemsInInventorySlot;
        _cachedIsEmpty = false;
    }

    internal void Clear()
    {
        if (isEmpty) return;
        item.state.amount = 0;
        item = null;
        _cachedIsEmpty = true;
    }

    internal void SwapItems(InventorySlot other)
    {
        if (isEmpty || other.isEmpty) return;
        (item, other.item) = (other.item, item);
        (capacity, other.capacity) = (other.capacity, capacity);
    }
}
