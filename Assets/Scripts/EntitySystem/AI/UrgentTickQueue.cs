using System;

public sealed class UrgentTickQueue
{
    private readonly EntityModel[] _items;
    private int _count;

    public UrgentTickQueue(int capacity) => _items = new EntityModel[Math.Max(capacity, 1)];

    public int Count => _count;
    public EntityModel At(int index) => _items[index];

    public void Enqueue(EntityModel model)
    {
        if (_count < _items.Length) _items[_count++] = model;
        else model.ClearTickDue();
    }

    public void Keep(int index, EntityModel model) => _items[index] = model;

    public void Truncate(int count)
    {
        if (count < _count) Array.Clear(_items, count, _count - count);
        _count = count;
    }
}
