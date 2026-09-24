using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public struct InstinctInput
{
    public float HpFraction;
    public float EnergyFraction;
    public float Stomach;
    public float Carrying;
    public float LocalDanger;
    public float Alarm;
    public float EntityDistance;
    public float FoodDistance;
    public byte  AtNest;
    public byte  Night;
    public byte  Warned;
}

public struct InstinctState
{
    public uint  Latches;
    public float Timer;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct InstinctJob : IJobFor
{
    public int Offset;
    [ReadOnly] public NativeArray<InstinctInput> Inputs;
    [NativeDisableParallelForRestriction] public NativeArray<InstinctState> States;
    [NativeDisableParallelForRestriction] public NativeArray<byte> NeedsDecision;

    public void Execute(int index)
    {
        int i = Offset + index;
        NeedsDecision[i] = 1;
    }
}

public sealed class InstinctSystem : IDisposable
{
    private NativeArray<InstinctInput> _inputs;
    private NativeArray<InstinctState> _states;
    private NativeArray<byte>          _needsDecision;

    public int Capacity => _inputs.IsCreated ? _inputs.Length : 0;

    public InstinctSystem(int capacity)
    {
        capacity       = Math.Max(capacity, 1);
        _inputs        = new NativeArray<InstinctInput>(capacity, Allocator.Persistent);
        _states        = new NativeArray<InstinctState>(capacity, Allocator.Persistent);
        _needsDecision = new NativeArray<byte>(capacity, Allocator.Persistent);
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)Capacity) return;
        _inputs[index]        = default;
        _states[index]        = default;
        _needsDecision[index] = 1;
    }

    public void Write(int index, in InstinctInput input)
    {
        if ((uint)index < (uint)Capacity) _inputs[index] = input;
    }

    public bool NeedsDecision(int index)
        => (uint)index >= (uint)Capacity || _needsDecision[index] != 0;

    public void Evaluate(int start, int end)
    {
        if (start < 0) start = 0;
        if (end > Capacity) end = Capacity;
        if (end <= start) return;

        new InstinctJob
        {
            Offset        = start,
            Inputs        = _inputs,
            States        = _states,
            NeedsDecision = _needsDecision,
        }.Run(end - start);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            _inputs[index]        = _inputs[last];
            _states[index]        = _states[last];
            _needsDecision[index] = _needsDecision[last];
        }
        _inputs[last]        = default;
        _states[last]        = default;
        _needsDecision[last] = 1;
    }

    public void Dispose()
    {
        if (_inputs.IsCreated)        _inputs.Dispose();
        if (_states.IsCreated)        _states.Dispose();
        if (_needsDecision.IsCreated) _needsDecision.Dispose();
    }
}
