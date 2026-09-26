using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public struct UtilityFeatures
{
    public const int Count = 12;

    public float Hunger;
    public float Fill;
    public float Storage;
    public float Threat;
    public float Night;
    public float Juveniles;
    public float Worker;
    public float Soldier;
    public float Scout;
    public float Nurse;
    public float HpDeficit;
    public float WinterApproach;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public unsafe struct UtilityPriorJob : IJobFor
{
    public int Offset;
    public int Plans;
    [ReadOnly] public NativeArray<UtilityFeatures> Inputs;
    [ReadOnly] public NativeArray<float> Weights;
    [NativeDisableParallelForRestriction] public NativeArray<float> Outputs;

    public void Execute(int index)
    {
        int i = Offset + index;
        var x = Inputs[i];
        float* f = (float*)UnsafeUtility.AddressOf(ref x);
        float* w = (float*)Weights.GetUnsafeReadOnlyPtr();
        float* o = (float*)Outputs.GetUnsafePtr() + i * Plans;

        for (int p = 0; p < Plans; p++)
        {
            float* row = w + p * UtilityFeatures.Count;
            float sum = 0f;
            for (int k = 0; k < UtilityFeatures.Count; k++) sum += row[k] * f[k];
            o[p] = sum;
        }
    }
}

public sealed class UtilityPriorSystem : IDisposable
{
    private const int Plans = PlanCatalog.Count;

    private NativeArray<UtilityFeatures> _inputs;
    private NativeArray<float> _outputs;
    private NativeArray<float> _weights;

    public int Capacity => _inputs.IsCreated ? _inputs.Length : 0;

    public UtilityPriorSystem(int capacity)
    {
        capacity = Math.Max(capacity, 1);
        _inputs  = new NativeArray<UtilityFeatures>(capacity, Allocator.Persistent);
        _outputs = new NativeArray<float>(capacity * Plans, Allocator.Persistent);
        _weights = new NativeArray<float>(Plans * UtilityFeatures.Count, Allocator.Persistent);
    }

    public void Write(int index, in UtilityFeatures features)
    {
        if ((uint)index < (uint)Capacity) _inputs[index] = features;
    }

    public ReadOnlySpan<float> Output(int index)
        => (uint)index < (uint)Capacity ? _outputs.AsReadOnlySpan().Slice(index * Plans, Plans) : ReadOnlySpan<float>.Empty;

    public void Evaluate(int start, int end)
    {
        if (start < 0) start = 0;
        if (end > Capacity) end = Capacity;
        if (end <= start) return;

        _weights.CopyFrom(SimulationRules.Frame.UtilityWeights);
        new UtilityPriorJob
        {
            Offset  = start,
            Plans   = Plans,
            Inputs  = _inputs,
            Weights = _weights,
            Outputs = _outputs,
        }.Run(end - start);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            _inputs[index] = _inputs[last];
            NativeArray<float>.Copy(_outputs, last * Plans, _outputs, index * Plans, Plans);
        }
        _inputs[last] = default;
    }

    public void Dispose()
    {
        if (_inputs.IsCreated)  _inputs.Dispose();
        if (_outputs.IsCreated) _outputs.Dispose();
        if (_weights.IsCreated) _weights.Dispose();
    }
}
