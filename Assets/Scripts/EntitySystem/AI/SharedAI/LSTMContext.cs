using System;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class LSTMContext : IDisposable
{
    public readonly int HiddenSize;

    private readonly FloatSlabPool _pool;
    private int _base;

    public LSTMContext(int inputSize, int hiddenSize)
    {
        HiddenSize = hiddenSize;
        _pool = ContextSlabs.Get(hiddenSize << 1);
        _base = _pool.Rent();
    }

    public float* Ptr => _pool.Ptr + _base;

    public Span<float> H => new Span<float>(Ptr, HiddenSize);
    public Span<float> C => new Span<float>(Ptr + HiddenSize, HiddenSize);

    public void Reset()
    {
        if (_base < 0) return;
        UnsafeUtility.MemClear(Ptr, (long)(HiddenSize << 1) * sizeof(float));
    }

    public void Dispose()
    {
        if (_base < 0) return;
        _pool.Return(_base);
        _base = -1;
    }
}