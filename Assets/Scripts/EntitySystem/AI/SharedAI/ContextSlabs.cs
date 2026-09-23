using System.Collections.Generic;

public static class ContextSlabs
{
    private static readonly Dictionary<int, FloatSlabPool> Pools = new();
    private static int _initialCapacity = 1;

    public static void Reserve(int capacity)
    {
        if (capacity > _initialCapacity) _initialCapacity = capacity;
    }

    public static FloatSlabPool Get(int stride)
    {
        if (!Pools.TryGetValue(stride, out var pool))
        {
            pool = new FloatSlabPool(stride, _initialCapacity);
            Pools.Add(stride, pool);
        }
        return pool;
    }

    public static void DisposeAll()
    {
        foreach (var pool in Pools.Values) pool.Dispose();
        Pools.Clear();
    }
}