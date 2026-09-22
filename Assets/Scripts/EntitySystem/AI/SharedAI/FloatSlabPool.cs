using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class FloatSlabPool : IDisposable
{
    public readonly int Stride;
    public readonly int Capacity;

    private NativeArray<float> _data;
    private float* _ptr;
    private int[]  _free;
    private int    _freeCount;

    public NativeArray<float> Data => _data;
    public float* Ptr => _ptr;
    public int Available => _freeCount;

    public FloatSlabPool(int stride, int capacity)
    {
        Stride   = stride;
        Capacity = capacity;

        _data = new NativeArray<float>(stride * capacity, Allocator.Persistent,
                                       NativeArrayOptions.ClearMemory);
        _ptr  = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(_data);

        _free = new int[capacity];
        for (int i = 0; i < capacity; i++) _free[i] = (capacity - 1 - i) * stride;
        _freeCount = capacity;
    }

    public int Rent()
    {
        if (_freeCount == 0) return -1;
        int baseOffset = _free[--_freeCount];
        UnsafeUtility.MemClear(_ptr + baseOffset, (long)Stride * sizeof(float));
        return baseOffset;
    }

    public void Return(int baseOffset)
    {
        if (baseOffset < 0 || _freeCount >= Capacity) return;
        _free[_freeCount++] = baseOffset;
    }

    public void Dispose()
    {
        if (_data.IsCreated) _data.Dispose();
        _ptr = null;
        _free = null;
        _freeCount = 0;
    }
}