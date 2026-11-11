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
    public float2 Start;
    public float2 Control;
    public float2 Goal;
    public float2 Velocity;
    public float  T;
    public float  InvLength;
    public float  CurrentSpeed;
    public float  LowSpeedTime;
    public double Deadline;
    public float  Speed;
    public float  EnergyCost;
    public float  SpeedModifier;
    public float  WaterSpeed;
    public float  WaterDrain;
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
    public byte   Blocked;
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
        long key  = long.MinValue;
        int  slot = -1;
        return Sample(wx, wz, ref key, ref slot);
    }

    public float SpeedModifier(float x, float z, float2 dir)
    {
        const float s = MapGenerator.scale;
        long key  = long.MinValue;
        int  slot = -1;
        float3 normal = MapGenerator.NormalFromHeights(
            Sample(x - s, z, ref key, ref slot), Sample(x + s, z, ref key, ref slot),
            Sample(x, z - s, ref key, ref slot), Sample(x, z + s, ref key, ref slot));
        return MapGenerator.SpeedFromNormal(normal, dir);
    }

    private float Sample(float wx, float wz, ref long cachedKey, ref int cachedSlot)
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

        if ((uint)x0 < (uint)(size - 1) && (uint)z0 < (uint)(size - 1))
        {
            long key = Position2Int.Pack(cx, cz);
            if (key != cachedKey)
            {
                cachedKey  = key;
                cachedSlot = Index.TryGetValue(key, out int found) ? found : -1;
            }
            if (cachedSlot < 0) return 0f;

            float* h = Chunks[cachedSlot].Heights;
            int i0 = z0 * size + x0;
            return math.lerp(
                math.lerp(h[i0],        h[i0 + 1],        tx),
                math.lerp(h[i0 + size], h[i0 + size + 1], tx),
                tz);
        }

        return math.lerp(
            math.lerp(Cell(cx, cz, x0,     z0),     Cell(cx, cz, x0 + 1, z0),     tx),
            math.lerp(Cell(cx, cz, x0,     z0 + 1), Cell(cx, cz, x0 + 1, z0 + 1), tx),
            tz);
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
    [ReadOnly] public NativeArray<int> Owners;
    [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
    public HeightAtlas Atlas;
    public double Now;
    public float  Dt;
    public float  MinSpeedModifier;
    public int    SpeedResampleFrames;
    public float  BlockedSpeedThreshold;
    public float  BlockedSeconds;
    public float  WaterLevel;
    public float  WaterSpeedMul;
    public float  WaterDrainMul;

    public void Execute(int index)
    {
        var s = States[index];
        if (s.Done != 0) return;

        int    owner = Owners[index];
        float3 pos   = Positions[owner];
        float2 toGoal = s.Goal - pos.xz;

        if (s.T >= 1f || math.lengthsq(toGoal) <= s.Arrive2)
        {
            Finish(ref s, 1, 0);
            States[index] = s;
            return;
        }

        if (Now >= s.Deadline)
        {
            Finish(ref s, 0, 0);
            States[index] = s;
            return;
        }

        float  t   = s.T;
        float  u   = 1f - t;
        float2 tan = 2f * (u * (s.Control - s.Start) + t * (s.Goal - s.Control));
        float  tl2 = math.lengthsq(tan);
        float2 dir = tl2 > 1e-8f ? tan * math.rsqrt(tl2) : math.normalizesafe(toGoal);

        if (s.Frame == 0)
        {
            s.SpeedModifier = Atlas.SpeedModifier(pos.x, pos.z, dir);
            float ground = Atlas.SampleHeight(pos.x, pos.z);
            s.WaterSpeed = MapGenerator.WaterModifier(ground, WaterLevel, WaterSpeedMul);
            s.WaterDrain = MapGenerator.WaterModifier(ground, WaterLevel, WaterDrainMul);
        }
        if (++s.Frame >= SpeedResampleFrames) s.Frame = 0;

        if (s.SpeedModifier < BlockedSpeedThreshold) s.LowSpeedTime += Dt;
        else s.LowSpeedTime = 0f;

        if (s.LowSpeedTime >= BlockedSeconds)
        {
            Finish(ref s, 0, 1);
            States[index] = s;
            return;
        }

        float costModifier = math.max(s.SpeedModifier, MinSpeedModifier);
        s.CurrentSpeed = math.lerp(s.CurrentSpeed, s.Speed * s.SpeedModifier * s.WaterSpeed, math.saturate(s.VelocityLerp * Dt));

        float advance = s.CurrentSpeed * Dt;
        float dT = tl2 > 1e-8f ? advance * math.rsqrt(tl2) : advance * s.InvLength;
        t = math.min(t + dT, 1f);
        u = 1f - t;
        s.T = t;

        float2 next = u * u * s.Start + 2f * u * t * s.Control + t * t * s.Goal;
        float2 step = next - pos.xz;

        s.Velocity = step * (1f / math.max(Dt, 1e-5f));
        pos.x = next.x;
        pos.z = next.y;
        pos.y = Atlas.SampleHeight(pos.x, pos.z) + s.HeightOffset;
        Positions[owner] = pos;

        float len = math.length(step);
        s.Distance += len;
        s.PathCost += len / costModifier;

        if (s.EnergyCost > 0f)
        {
            float spent = s.EnergyCost / costModifier * s.WaterDrain * Dt;
            s.Energy  += spent;
            s.Pending += spent;
        }

        States[index] = s;
    }

    private static void Finish(ref MotionState s, byte arrived, byte blocked)
    {
        s.Arrived  = arrived;
        s.Blocked  = blocked;
        s.Done     = 1;
        s.Velocity = float2.zero;
    }
}

[BurstCompile]
public struct MotionApplyJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<int> Owners;
    [ReadOnly] public NativeArray<float3> Positions;

    public void Execute(int index, TransformAccess transform)
        => transform.position = Positions[Owners[index]];
}

public sealed unsafe class MotionSystem : IDisposable
{
    private const float MinSpeedModifier    = 0.05f;
    private const int   SpeedResampleFrames = 4;
    private const float AtlasMarginCells    = 3f;
    private const int   JobBatch            = 16;

    private static readonly int4 Unwindowed = new int4(int.MinValue);

    private NativeList<MotionState>          _states;
    private NativeList<int>                  _owners;
    private NativeArray<float3>              _positions;
    private NativeArray<int>                 _motionOf;
    private TransformAccessArray             _transforms;
    private NativeParallelHashMap<long, int> _atlasIndex;
    private NativeList<HeightChunk>          _atlasChunks;

    private SurfaceMotor[] _motors;
    private int4[]         _windows;
    private int            _atlasStamp;
    private int            _lastTouchFrame;
    private MapGenerator   _map;
    private int    _count;
    private double _lastTime;
    private bool   _disposed;

    public int Count => _count;

    public MotionSystem(int capacity)
    {
        if (capacity < 1) capacity = 1;
        _states      = new NativeList<MotionState>(capacity, Allocator.Persistent);
        _owners      = new NativeList<int>(capacity, Allocator.Persistent);
        _positions   = new NativeArray<float3>(capacity, Allocator.Persistent);
        _motionOf    = new NativeArray<int>(capacity, Allocator.Persistent);
        for (int i = 0; i < capacity; i++) _motionOf[i] = -1;
        _transforms  = new TransformAccessArray(capacity);
        _atlasIndex  = new NativeParallelHashMap<long, int>(capacity, Allocator.Persistent);
        _atlasChunks = new NativeList<HeightChunk>(capacity, Allocator.Persistent);
        _motors      = new SurfaceMotor[capacity];
        _windows     = new int4[capacity];
        _lastTime    = SimulationClock.TimeD;
    }

    public void Inject(MapGenerator map)
    {
        _map        = map;
        _atlasStamp = int.MinValue;
    }

    public NativeArray<float3> Positions => _positions;

    public float3 PositionOf(int slot)
        => _positions.IsCreated && (uint)slot < (uint)_positions.Length ? _positions[slot] : float3.zero;

    public void SetPosition(int slot, float3 position)
    {
        if (_positions.IsCreated && (uint)slot < (uint)_positions.Length) _positions[slot] = position;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if (_disposed || !_positions.IsCreated) return;
        int cap = _positions.Length;
        if ((uint)index >= (uint)cap || (uint)last >= (uint)cap) return;

        int moving = _motionOf[index];
        if (moving >= 0) Finish(moving);

        if (index != last)
        {
            _positions[index] = _positions[last];
            int movedMotion = _motionOf[last];
            _motionOf[index] = movedMotion;
            if (movedMotion >= 0) _owners[movedMotion] = index;
        }
        _motionOf[last]  = -1;
        _positions[last] = default;
    }

    internal bool Start(SurfaceMotor motor, Transform tr, in MotionState state)
    {
        int slot = motor.Slot;
        if (_disposed || _map == null || (uint)slot >= (uint)_positions.Length) return false;

        int idx = motor.MotionIndex;
        if ((uint)idx < (uint)_count && ReferenceEquals(_motors[idx], motor))
        {
            var s = state;
            var prev = _states[idx];
            s.Pending     += prev.Pending;
            s.CurrentSpeed = prev.CurrentSpeed;
            _states[idx] = s;
            return true;
        }

        if (_count == _motors.Length)
        {
            int cap = Math.Max(_motors.Length << 1, _count + 1);
            Array.Resize(ref _motors, cap);
            Array.Resize(ref _windows, cap);
        }

        _states.Add(state);
        _owners.Add(slot);
        _transforms.Add(tr);
        _motionOf[slot] = _count;
        _motors[_count] = motor;
        _windows[_count] = Unwindowed;
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

        SyncAtlas();

        var states = _states.AsArray();

        var move = new MotionJob
        {
            States              = states,
            Owners              = _owners.AsArray(),
            Positions           = _positions,
            Atlas               = new HeightAtlas { Index = _atlasIndex, Chunks = _atlasChunks.AsArray() },
            Now                 = now,
            Dt                  = dt,
            MinSpeedModifier      = MinSpeedModifier,
            SpeedResampleFrames   = SpeedResampleFrames,
            BlockedSpeedThreshold = SimulationRules.Frame.BlockedSpeedThreshold,
            BlockedSeconds        = SimulationRules.Frame.BlockedSeconds,
            WaterLevel            = SimulationRules.Frame.WaterLevel,
            WaterSpeedMul         = SimulationRules.Frame.WaterSpeedMul,
            WaterDrainMul         = SimulationRules.Frame.WaterDrainMul,
        }.Schedule(_count, JobBatch);

        new MotionApplyJob { Owners = _owners.AsArray(), Positions = _positions }.Schedule(_transforms, move).Complete();

        for (int i = _count - 1; i >= 0; i--)
            if (_states[i].Done != 0) Finish(i);
    }

    public HeightAtlas PrepareAtlas(float2[] points, int count, float margin)
    {
        if (!_disposed && _map != null)
        {
            SyncAtlas();
            for (int i = 0; i < count; i++) AppendWindow(WindowOf(points[i].x, points[i].y, margin), false);
        }
        return new HeightAtlas { Index = _atlasIndex, Chunks = _atlasChunks.AsArray() };
    }

    private static int4 WindowOf(float x, float z, float margin)
    {
        const float invChunk = 1f / MapGenerator.chunkSize;
        return new int4(
            (int)math.floor((x - margin) * invChunk),
            (int)math.floor((z - margin) * invChunk),
            (int)math.floor((x + margin) * invChunk),
            (int)math.floor((z + margin) * invChunk));
    }

    private unsafe void AppendWindow(int4 w, bool generate)
    {
        for (int cz = w.y; cz <= w.w; cz++)
        for (int cx = w.x; cx <= w.z; cx++)
        {
            long key = Position2Int.Pack(cx, cz);
            if (_atlasIndex.ContainsKey(key)) continue;

            ChunkData chunk;
            if (generate) chunk = _map.GetMapData(cx, cz);
            else if (!_map.TryGetChunk(key, out chunk) || chunk == null) continue;

            _atlasIndex.Add(key, _atlasChunks.Length);
            _atlasChunks.Add(new HeightChunk { Heights = (float*)chunk.HeightRaw.GetUnsafeReadOnlyPtr() });
        }
    }

    private void SyncAtlas()
    {
        const float margin = AtlasMarginCells * MapGenerator.scale;

        int stamp;
        do
        {
            stamp = _map.UnloadStamp;
            if (stamp != _atlasStamp)
            {
                _atlasStamp = stamp;
                _atlasIndex.Clear();
                _atlasChunks.Clear();
                for (int i = 0; i < _count; i++) _windows[i] = Unwindowed;
            }

            for (int i = 0; i < _count; i++)
            {
                float3 p = _positions[_owners[i]];
                int4 w = WindowOf(p.x, p.z, margin);
                if (math.all(w == _windows[i])) continue;
                AppendWindow(w, true);
                _windows[i] = w;
            }
        }
        while (stamp != _map.UnloadStamp);

        int frame = Time.frameCount;
        if (frame - _lastTouchFrame < _map.EvictionTouchInterval) return;
        _lastTouchFrame = frame;
        for (int i = 0; i < _count; i++) TouchWindow(_windows[i]);
    }

    private void TouchWindow(int4 w)
    {
        for (int cz = w.y; cz <= w.w; cz++)
        for (int cx = w.x; cx <= w.z; cx++)
            _map.TouchChunk(Position2Int.Pack(cx, cz));
    }

    private void Finish(int idx)
    {
        var motor = _motors[idx];
        motor.OnMotionFinished(_states[idx]);

        int last = --_count;
        _motionOf[_owners[idx]] = -1;
        _states.RemoveAtSwapBack(idx);
        _owners.RemoveAtSwapBack(idx);
        _transforms.RemoveAtSwapBack(idx);

        if (idx != last)
        {
            var moved = _motors[last];
            _motors[idx] = moved;
            _windows[idx] = _windows[last];
            moved.MotionIndex = idx;
            _motionOf[_owners[idx]] = idx;
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
        if (_owners.IsCreated)       _owners.Dispose();
        if (_positions.IsCreated)    _positions.Dispose();
        if (_motionOf.IsCreated)     _motionOf.Dispose();
        if (_transforms.isCreated)   _transforms.Dispose();
        if (_atlasIndex.IsCreated)   _atlasIndex.Dispose();
        if (_atlasChunks.IsCreated)  _atlasChunks.Dispose();
    }
}