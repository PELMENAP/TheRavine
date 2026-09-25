using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct BoidAgent
{
    public float2 Position;
    public float2 Heading;
    public float3 Weights;
    public int    Colony;
    public byte   Active;
}

public struct BoidOutput
{
    public float2 Steer;
    public float2 MeanHeading;
    public float2 CentroidDir;
    public int    Neighbors;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct BoidBucketJob : IJob
{
    [ReadOnly] public NativeArray<BoidAgent> Agents;
    public NativeArray<int> CellEnd;
    public NativeArray<int> Items;
    public NativeArray<int> BucketOf;
    public int   Count;
    public int   Mask;
    public float InvCell;

    public void Execute()
    {
        int buckets = Mask + 1;
        for (int b = 0; b <= buckets; b++) CellEnd[b] = 0;

        for (int i = 0; i < Count; i++)
        {
            var a = Agents[i];
            if (a.Active == 0) { BucketOf[i] = -1; continue; }
            int2 c = (int2)math.floor(a.Position * InvCell);
            int  b = BoidHash.Bucket(c, Mask);
            BucketOf[i] = b;
            CellEnd[b + 1]++;
        }

        for (int b = 0; b < buckets; b++) CellEnd[b + 1] += CellEnd[b];

        for (int i = 0; i < Count; i++)
        {
            int b = BucketOf[i];
            if (b < 0) continue;
            Items[CellEnd[b]++] = i;
        }
    }
}

public static class BoidHash
{
    public static int Bucket(int2 c, int mask) => ((c.x * 73856093) ^ (c.y * 19349663)) & mask;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct BoidJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<BoidAgent> Agents;
    [ReadOnly] public NativeArray<int> CellEnd;
    [ReadOnly] public NativeArray<int> Items;
    [WriteOnly] public NativeArray<BoidOutput> Outputs;
    public int   Mask;
    public float InvCell;
    public float Radius;
    public float SeparationRadius;

    public unsafe void Execute(int i)
    {
        var self = Agents[i];
        if (self.Active == 0) { Outputs[i] = default; return; }

        int2 cell = (int2)math.floor(self.Position * InvCell);
        int* seen = stackalloc int[9];
        int  seenCount = 0;

        float r2 = Radius * Radius;
        float s2 = SeparationRadius * SeparationRadius;
        float2 sep = float2.zero, ali = float2.zero, coh = float2.zero;
        int n = 0;

        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int b = BoidHash.Bucket(cell + new int2(dx, dy), Mask);
            bool dup = false;
            for (int k = 0; k < seenCount; k++) if (seen[k] == b) { dup = true; break; }
            if (dup) continue;
            seen[seenCount++] = b;

            int start = b == 0 ? 0 : CellEnd[b - 1];
            int end   = CellEnd[b];
            for (int k = start; k < end; k++)
            {
                int j = Items[k];
                if (j == i) continue;
                var o = Agents[j];
                if (o.Colony != self.Colony) continue;

                float2 d  = self.Position - o.Position;
                float  d2 = math.lengthsq(d);
                if (d2 > r2) continue;

                if (d2 < s2 && d2 > 1e-6f) sep += d / d2;
                ali += o.Heading;
                coh += o.Position;
                n++;
            }
        }

        if (n == 0) { Outputs[i] = default; return; }

        float2 meanHeading = math.normalizesafe(ali);
        float2 centroidDir = math.normalizesafe(coh / n - self.Position);
        float2 steer = self.Weights.x * math.normalizesafe(sep)
                     + self.Weights.y * meanHeading
                     + self.Weights.z * centroidDir;

        Outputs[i] = new BoidOutput
        {
            Steer       = steer,
            MeanHeading = meanHeading,
            CentroidDir = centroidDir,
            Neighbors   = n,
        };
    }
}

public sealed class BoidSystem : IDisposable
{
    private NativeArray<BoidAgent>  _agents;
    private NativeArray<BoidOutput> _outputs;
    private NativeArray<int> _cellEnd;
    private NativeArray<int> _items;
    private NativeArray<int> _bucketOf;
    private readonly int _mask;

    public int Capacity => _agents.IsCreated ? _agents.Length : 0;

    public BoidSystem(int capacity)
    {
        capacity  = Math.Max(capacity, 1);
        _mask     = math.ceilpow2(capacity * 2) - 1;
        _agents   = new NativeArray<BoidAgent>(capacity, Allocator.Persistent);
        _outputs  = new NativeArray<BoidOutput>(capacity, Allocator.Persistent);
        _cellEnd  = new NativeArray<int>(_mask + 2, Allocator.Persistent);
        _items    = new NativeArray<int>(capacity, Allocator.Persistent);
        _bucketOf = new NativeArray<int>(capacity, Allocator.Persistent);
    }

    public void Write(int index, in BoidAgent agent)
    {
        if ((uint)index < (uint)Capacity) _agents[index] = agent;
    }

    public BoidOutput Output(int index)
        => (uint)index < (uint)Capacity ? _outputs[index] : default;

    public void Run(int count)
    {
        count = math.min(count, Capacity);
        if (count <= 0) return;

        ref readonly var r = ref SimulationRules.Frame;
        float radius  = math.max(r.BoidRadius, 0.1f);
        float invCell = 1f / radius;

        var bucket = new BoidBucketJob
        {
            Agents   = _agents,
            CellEnd  = _cellEnd,
            Items    = _items,
            BucketOf = _bucketOf,
            Count    = count,
            Mask     = _mask,
            InvCell  = invCell,
        }.Schedule();

        new BoidJob
        {
            Agents           = _agents,
            CellEnd          = _cellEnd,
            Items            = _items,
            Outputs          = _outputs,
            Mask             = _mask,
            InvCell          = invCell,
            Radius           = radius,
            SeparationRadius = r.BoidSeparationRadius,
        }.Schedule(count, 16, bucket).Complete();
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            _agents[index]  = _agents[last];
            _outputs[index] = _outputs[last];
        }
        _agents[last]  = default;
        _outputs[last] = default;
    }

    public void Dispose()
    {
        if (_agents.IsCreated)   _agents.Dispose();
        if (_outputs.IsCreated)  _outputs.Dispose();
        if (_cellEnd.IsCreated)  _cellEnd.Dispose();
        if (_items.IsCreated)    _items.Dispose();
        if (_bucketOf.IsCreated) _bucketOf.Dispose();
    }
}
