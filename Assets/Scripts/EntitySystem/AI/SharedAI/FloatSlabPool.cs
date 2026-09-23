using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class FloatSlabPool : IDisposable
{
    public readonly int Stride;
    public int Capacity { get; private set; }

    private NativeArray<float> _data;
    private float* _ptr;
    private int[]  _free;
    private int    _freeCount;

    public NativeArray<float> Data => _data;
    public float* Ptr => _ptr;
    public int Available => _freeCount;

    public FloatSlabPool(int stride, int capacity)
    {
        Stride = stride;
        _free  = Array.Empty<int>();
        Grow(capacity < 1 ? 1 : capacity);
    }

    public int Rent()
    {
        if (_free == null) return -1;
        if (_freeCount == 0) Grow(Capacity << 1);

        int baseOffset = _free[--_freeCount];
        UnsafeUtility.MemClear(_ptr + baseOffset, (long)Stride * sizeof(float));
        return baseOffset;
    }

    public void Return(int baseOffset)
    {
        if (_free == null || baseOffset < 0 || _freeCount >= Capacity) return;
        _free[_freeCount++] = baseOffset;
    }

    private void Grow(int capacity)
    {
        int old = Capacity;
        if (capacity <= old) return;

        var data = new NativeArray<float>(Stride * capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        if (_data.IsCreated)
        {
            NativeArray<float>.Copy(_data, data, Stride * old);
            _data.Dispose();
        }

        _data = data;
        _ptr  = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(_data);

        if (_free.Length < capacity) Array.Resize(ref _free, capacity);
        for (int i = capacity - 1; i >= old; i--) _free[_freeCount++] = i * Stride;

        Capacity = capacity;
    }

    public void Dispose()
    {
        if (_data.IsCreated) _data.Dispose();
        _ptr = null;
        _free = null;
        _freeCount = 0;
        Capacity = 0;
    }
}