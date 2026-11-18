using System;
using Unity.Collections;
using Unity.Mathematics;

public sealed class NoveltyProgressSystem : IDisposable
{
    private readonly int _capacity;
    private NativeArray<float> _fast;
    private NativeArray<float> _slow;
    private NativeArray<byte>  _primed;

    public int Capacity => _capacity;

    public NoveltyProgressSystem(int capacity)
    {
        _capacity = math.max(capacity, 1);
        _fast   = new NativeArray<float>(_capacity, Allocator.Persistent);
        _slow   = new NativeArray<float>(_capacity, Allocator.Persistent);
        _primed = new NativeArray<byte>(_capacity, Allocator.Persistent);
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)_capacity) return;
        _fast[index]   = 0f;
        _slow[index]   = 0f;
        _primed[index] = 0;
    }

    public float Fast(int index) => _fast[index];
    public float Slow(int index) => _slow[index];

    public float Step(int index, float error, float fastRate, float slowRate)
    {
        if ((uint)index >= (uint)_capacity || !math.isfinite(error)) return 0f;

        if (_primed[index] == 0)
        {
            _fast[index]   = error;
            _slow[index]   = error;
            _primed[index] = 1;
            return 0f;
        }

        float fast = _fast[index] + fastRate * (error - _fast[index]);
        float slow = _slow[index] + slowRate * (error - _slow[index]);
        _fast[index] = fast;
        _slow[index] = slow;
        return math.max(0f, slow - fast);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            _fast[index]   = _fast[last];
            _slow[index]   = _slow[last];
            _primed[index] = _primed[last];
        }
        Reset(last);
    }

    public void Dispose()
    {
        if (_fast.IsCreated)   _fast.Dispose();
        if (_slow.IsCreated)   _slow.Dispose();
        if (_primed.IsCreated) _primed.Dispose();
    }
}
