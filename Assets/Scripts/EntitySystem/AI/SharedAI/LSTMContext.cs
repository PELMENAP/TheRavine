using System;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class LSTMContext : IDisposable
{
    public readonly int HiddenSize;
    public readonly int InputSize;

    private readonly FloatSlabPool _pool;
    private int _base;

    public float NextStepTime;
    public bool  EmaPrimed;

    public LSTMContext(int inputSize, int hiddenSize)
    {
        HiddenSize = hiddenSize;
        InputSize  = inputSize;
        _pool = ContextSlabs.Get((hiddenSize << 1) + inputSize);
        _base = _pool.Rent();
    }

    public float* Ptr => _pool.Ptr + _base;
    public float* Ema => _pool.Ptr + _base + (HiddenSize << 1);

    public Span<float> H => new Span<float>(Ptr, HiddenSize);
    public Span<float> C => new Span<float>(Ptr + HiddenSize, HiddenSize);

    public void Reset()
    {
        if (_base < 0) return;
        UnsafeUtility.MemClear(Ptr, (long)((HiddenSize << 1) + InputSize) * sizeof(float));
        NextStepTime = 0f;
        EmaPrimed    = false;
    }

    public void Dispose()
    {
        if (_base < 0) return;
        _pool.Return(_base);
        _base = -1;
    }
}
