using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using TheRavine.Extensions;
using TheRavine.Generator;

public struct MotionState
{
    public float3 Position;
    public float2 Goal;
    public float2 Velocity;
    public double Deadline;
    public float  Speed;
    public float  EnergyCost;
    public float  SpeedModifier;
    public float  Arrive2;
    public float  HeightOffset;
    public float  VelocityLerp;
    public float  Distance;
    public float  PathCost;
    public float  Energy;
    public float  Pending;
    public int    Frame;
    public byte   Done;
    public byte   Arrived;
}

public unsafe struct HeightChunk
{
    public float* Heights;
}

public unsafe struct HeightAtlas
{
    [ReadOnly] public NativeParallelHashMap<long, int> Index;
    [ReadOnly] public NativeArray<HeightChunk> Chunks;

    public float SampleHeight(float wx, float wz)
    {
        const int size = MapGenerator.mapChunkSize;

        float gx = wx / MapGenerator.scale;
        float gz = wz / MapGenerator.scale;

        int cx = (int)math.floor(gx / size);
        int cz = (int)math.floor(gz / size);

        float lxf = gx - cx * size;
        float lzf = gz - cz * size;

        int x0 = (int)math.floor(lxf);
        int z0 = (int)math.floor(lzf);

        float tx = lxf - x0;
        float tz = lzf - z0;

        return math.lerp(
            math.lerp(Cell(cx, cz, x0,     z0),     Cell(cx, cz, x0 + 1, z0),     tx),
            math.lerp(Cell(cx, cz, x0,     z0 + 1), Cell(cx, cz, x0 + 1, z0 + 1), tx),
            tz);
    }

    public float SpeedModifier(float x, float z, float2 dir)
    {
        const float s = MapGenerator.scale;
        float3 normal = MapGenerator.NormalFromHeights(
            SampleHeight(x - s, z), SampleHeight(x + s, z),
            SampleHeight(x, z - s), SampleHeight(x, z + s));
        return MapGenerator.SpeedFromNormal(normal, dir);
    }

    private float Cell(int cx, int cz, int lx, int lz)
    {
        const int size = MapGenerator.mapChunkSize;
        if (lx >= size) { lx -= size; cx++; }
        if (lz >= size) { lz -= size; cz++; }

        if (!Index.TryGetValue(Position2Int.Pack(cx, cz), out int slot)) return 0f;
        return Chunks[slot].Heights[lz * size + lx];
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct MotionJob : IJobParallelFor
{
    public NativeArray<MotionState> States;
    public HeightAtlas Atlas;
    public double Now;
    public float  Dt;
    public float  MinSpeedModifier;
    public int    SpeedResampleFrames;

    public void Execute(int index)
    {
        var s = States[index];
        if (s.Done != 0) return;

        float3 pos = s.Position;
        float2 to  = s.Goal - pos.xz;
        float  d2  = math.lengthsq(to);

        if (d2 <= s.Arrive2 || d2 < 1e-6f)
        {
            s.Arrived  = 1;
            s.Done     = 1;
            s.Velocity = float2.zero;
            States[index] = s;
            return;
        }

        if (Now >= s.Deadline)
        {
            s.Done     = 1;
            s.Velocity = float2.zero;
            States[index] = s;
            return;
        }

        float2 dir = to * math.rsqrt(d2);

        if (s.Frame == 0) s.SpeedModifier = Atlas.SpeedModifier(pos.x, pos.z, dir);
        if (++s.Frame >= SpeedResampleFrames) s.Frame = 0;

        float costModifier = math.max(s.SpeedModifier, MinSpeedModifier);

        s.Velocity = math.lerp(s.Velocity, dir * (s.Speed * s.SpeedModifier),
            math.saturate(s.VelocityLerp * Dt));

        float2 step = s.Velocity * Dt;
        pos.x += step.x;
        pos.z += step.y;
        pos.y  = Atlas.SampleHeight(pos.x, pos.z) + s.HeightOffset;
        s.Position = pos;

        float len = math.length(step);
        s.Distance += len;
        s.PathCost += len / costModifier;

        if (s.EnergyCost > 0f)
        {
            float spent = s.EnergyCost / costModifier * Dt;
            s.Energy  += spent;
            s.Pending += spent;
        }

        States[index] = s;
    }
}

[BurstCompile]
public struct MotionApplyJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<MotionState> States;

    public void Execute(int index, TransformAccess transform)
        => transform.position = States[index].Position;
}

public sealed unsafe class MotionSystem : IDisposable
{
    private const float MinSpeedModifier    = 0.05f;
    private const int   SpeedResampleFrames = 4;
    private const float AtlasMarginCells    = 3f;
    private const int   JobBatch            = 16;

    private NativeList<MotionState>          _states;
    private TransformAccessArray             _transforms;
    private NativeParallelHashMap<long, int> _atlasIndex;
    private NativeList<HeightChunk>          _atlasChunks;

    private SurfaceMotor[] _motors;
    private MapGenerator   _map;
    private int    _count;
    private double _lastTime;
    private bool   _disposed;

    public int Count => _count;

    public MotionSystem(int capacity)
    {
        if (capacity < 1) capacity = 1;
        _states      = new NativeList<MotionState>(capacity, Allocator.Persistent);
        _transforms  = new TransformAccessArray(capacity);
        _atlasIndex  = new NativeParallelHashMap<long, int>(capacity, Allocator.Persistent);
        _atlasChunks = new NativeList<HeightChunk>(capacity, Allocator.Persistent);
        _motors      = new SurfaceMotor[capacity];
        _lastTime    = SimulationClock.TimeD;
    }

    public void Inject(MapGenerator map) => _map = map;

    internal bool Start(SurfaceMotor motor, Transform tr, in MotionState state)
    {
        if (_disposed || _map == null) return false;

        int idx = motor.MotionIndex;
        if ((uint)idx < (uint)_count && ReferenceEquals(_motors[idx], motor))
        {
            var s = state;
            s.Pending += _states[idx].Pending;
            _states[idx] = s;
            return true;
        }

        if (_count == _motors.Length)
            Array.Resize(ref _motors, Math.Max(_motors.Length << 1, _count + 1));

        _states.Add(state);
        _transforms.Add(tr);
        _motors[_count] = motor;
        motor.MotionIndex = _count;
        _count++;
        return true;
    }

    internal void Stop(SurfaceMotor motor)
    {
        int idx = motor.MotionIndex;
        if (_disposed || (uint)idx >= (uint)_count || !ReferenceEquals(_motors[idx], motor))
        {
            motor.MotionIndex = -1;
            return;
        }
        Finish(idx);
    }

    internal float2 VelocityOf(int index)
        => (uint)index < (uint)_count ? _states[index].Velocity : float2.zero;

    internal float TakePending(int index)
    {
        if ((uint)index >= (uint)_count) return 0f;
        var s = _states[index];
        float p = s.Pending;
        s.Pending = 0f;
        _states[index] = s;
        return p;
    }

    public void Step()
    {
        double now = SimulationClock.TimeD;
        float  dt  = (float)(now - _lastTime);
        _lastTime = now;

        if (_disposed || _count == 0 || dt <= 0f) return;

        float maxDt = SimulationRules.Frame.MaxCycleDt;
        if (dt > maxDt) dt = maxDt;

        BuildAtlas();

        var states = _states.AsArray();

        var move = new MotionJob
        {
            States              = states,
            Atlas               = new HeightAtlas { Index = _atlasIndex, Chunks = _atlasChunks.AsArray() },
            Now                 = now,
            Dt                  = dt,
            MinSpeedModifier    = MinSpeedModifier,
            SpeedResampleFrames = SpeedResampleFrames,
        }.Schedule(_count, JobBatch);

        new MotionApplyJob { States = states }.Schedule(_transforms, move).Complete();

        for (int i = _count - 1; i >= 0; i--)
            if (_states[i].Done != 0) Finish(i);
    }

    private void BuildAtlas()
    {
        _atlasIndex.Clear();
        _atlasChunks.Clear();

        const float margin   = AtlasMarginCells * MapGenerator.scale;
        const float invChunk = 1f / MapGenerator.chunkSize;

        for (int i = 0; i < _count; i++)
        {
            float3 p = _states[i].Position;

            int x0 = (int)math.floor((p.x - margin) * invChunk);
            int x1 = (int)math.floor((p.x + margin) * invChunk);
            int z0 = (int)math.floor((p.z - margin) * invChunk);
            int z1 = (int)math.floor((p.z + margin) * invChunk);

            for (int cz = z0; cz <= z1; cz++)
            for (int cx = x0; cx <= x1; cx++)
            {
                long key = Position2Int.Pack(cx, cz);
                if (_atlasIndex.ContainsKey(key)) continue;

                var chunk = _map.GetMapData(cx, cz);
                _atlasIndex.Add(key, _atlasChunks.Length);
                _atlasChunks.Add(new HeightChunk { Heights = (float*)chunk.HeightRaw.GetUnsafeReadOnlyPtr() });
            }
        }
    }

    private void Finish(int idx)
    {
        var motor = _motors[idx];
        motor.OnMotionFinished(_states[idx]);

        int last = --_count;
        _states.RemoveAtSwapBack(idx);
        _transforms.RemoveAtSwapBack(idx);

        if (idx != last)
        {
            var moved = _motors[last];
            _motors[idx] = moved;
            moved.MotionIndex = idx;
        }
        _motors[last] = null;
        motor.MotionIndex = -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _count; i++)
            if (_motors[i] != null) _motors[i].MotionIndex = -1;
        _count = 0;

        if (_states.IsCreated)       _states.Dispose();
        if (_transforms.isCreated)   _transforms.Dispose();
        if (_atlasIndex.IsCreated)   _atlasIndex.Dispose();
        if (_atlasChunks.IsCreated)  _atlasChunks.Dispose();
    }
}