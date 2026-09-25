using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public enum ColonyChannel : byte
{
    Food     = 0,
    Danger   = 1,
    Trail    = 2,
    KinDeath = 3,
    Explored = 4,
    Count,
}

public unsafe struct ColonyFieldView
{
    [NativeDisableUnsafePtrRestriction] public float* Data;
    public float2 Origin;
    public float  InvCell;
    public int    Size;
    public int    Cells;

    public readonly bool TryIndex(float2 p, out int index)
    {
        int2 c = (int2)math.floor((p - Origin) * InvCell);
        if (math.any(c < 0) || math.any(c >= Size)) { index = -1; return false; }
        index = c.y * Size + c.x;
        return true;
    }

    public readonly float At(ColonyChannel channel, int index) => Data[(int)channel * Cells + index];

    public readonly float FieldAt(ColonyChannel channel, float2 p) => TryIndex(p, out int i) ? At(channel, i) : 0f;
}

public static class ColonyFieldStats
{
    public const int PeakIndex    = 0;
    public const int PeakValue    = 1;
    public const int FoodMass     = 2;
    public const int FoodX        = 3;
    public const int FoodY        = 4;
    public const int DangerMass   = 5;
    public const int DangerX      = 6;
    public const int DangerY      = 7;
    public const int Count        = 8;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct ColonyFieldDecayJob : IJob
{
    public NativeArray<float> Field;
    [ReadOnly] public NativeArray<float> Keep;
    [WriteOnly] public NativeArray<float> Stats;
    public int Cells;
    public int Size;

    public void Execute()
    {
        int channels = Keep.Length;
        for (int c = 0; c < channels; c++)
        {
            float k   = Keep[c];
            int   off = c * Cells;
            for (int i = 0; i < Cells; i++) Field[off + i] *= k;
        }

        int   best    = -1;
        float bestVal = 0f;
        float mass = 0f, mx = 0f, my = 0f;
        float dMass = 0f, dx = 0f, dy = 0f;
        int   fo = (int)ColonyChannel.Food * Cells;
        int   dO = (int)ColonyChannel.Danger * Cells;

        for (int i = 0; i < Cells; i++)
        {
            float x = i % Size + 0.5f;
            float y = i / Size + 0.5f;

            float f = Field[fo + i];
            if (f > bestVal) { bestVal = f; best = i; }
            mass += f; mx += f * x; my += f * y;

            float d = Field[dO + i];
            dMass += d; dx += d * x; dy += d * y;
        }

        Stats[ColonyFieldStats.PeakIndex]  = best;
        Stats[ColonyFieldStats.PeakValue]  = bestVal;
        Stats[ColonyFieldStats.FoodMass]   = mass;
        Stats[ColonyFieldStats.FoodX]      = mass > 1e-6f ? mx / mass : 0f;
        Stats[ColonyFieldStats.FoodY]      = mass > 1e-6f ? my / mass : 0f;
        Stats[ColonyFieldStats.DangerMass] = dMass;
        Stats[ColonyFieldStats.DangerX]    = dMass > 1e-6f ? dx / dMass : 0f;
        Stats[ColonyFieldStats.DangerY]    = dMass > 1e-6f ? dy / dMass : 0f;
    }
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct ColonyFieldShiftJob : IJob
{
    [ReadOnly] public NativeArray<float> Source;
    [WriteOnly] public NativeArray<float> Target;
    public int Cells;
    public int Size;
    public int Channels;
    public int2 Offset;

    public void Execute()
    {
        for (int c = 0; c < Channels; c++)
        {
            int off = c * Cells;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int sx = x + Offset.x;
                int sy = y + Offset.y;
                bool inside = sx >= 0 && sy >= 0 && sx < Size && sy < Size;
                Target[off + y * Size + x] = inside ? Source[off + sy * Size + sx] : 0f;
            }
        }
    }
}

public sealed unsafe class NestState : IDisposable
{
    public const int ChannelCount = (int)ColonyChannel.Count;

    public float2 Position { get; private set; }
    public float  Radius   { get; private set; }

    public float Storage;
    public float Alarm { get; private set; }
    public float Hunger { get; private set; }
    public bool  AlarmHunt { get; private set; }

    public float2 FoodPeak { get; private set; }
    public bool   HasFoodPeak { get; private set; }
    public float2 Migration { get; private set; }
    public float  MigrationPressure => math.length(Migration);
    public float  DangerAtNest { get; private set; }
    public int    Relocations { get; private set; }

    private NativeArray<float> _field;
    private NativeArray<float> _scratch;
    private NativeArray<float> _keep;
    private NativeArray<float> _stats;
    private ColonyFieldView _view;
    private readonly int   _size;
    private readonly int   _cells;
    private readonly float _cell;
    private float2 _origin;

    public NestState(float2 position)
    {
        var r = SimulationRules.Active;
        Position = position;
        Radius   = r.NestRadius;
        _size    = math.max(4, r.NestMapSize);
        _cells   = _size * _size;
        _cell    = math.max(0.5f, r.NestMapCellSize);
        _origin  = position - 0.5f * _size * _cell;

        _field   = new NativeArray<float>(_cells * ChannelCount, Allocator.Persistent);
        _scratch = new NativeArray<float>(_cells * ChannelCount, Allocator.Persistent);
        _keep    = new NativeArray<float>(ChannelCount, Allocator.Persistent);
        _stats   = new NativeArray<float>(ColonyFieldStats.Count, Allocator.Persistent);

        RefreshView();
    }

    private void RefreshView()
    {
        _view = new ColonyFieldView
        {
            Data    = (float*)NativeArrayUnsafeUtility.GetUnsafePtr(_field),
            Origin  = _origin,
            InvCell = 1f / _cell,
            Size    = _size,
            Cells   = _cells,
        };
    }

    public ColonyFieldView View => _view;

    public bool Contains(float2 p) => math.distancesq(p, Position) <= Radius * Radius;

    public void Mark(ColonyChannel channel, float2 p, float amount)
    {
        if (!_view.TryIndex(p, out int i)) return;
        float* cell = _view.Data + (int)channel * _cells + i;
        *cell = math.min(*cell + amount, SimulationRules.Frame.NestMapMaxValue);
    }

    public float FieldAt(ColonyChannel channel, float2 p) => _view.FieldAt(channel, p);

    public float SampleDirection(float2 origin, float radius, int samples, float phase,
        float foodWeight, float dangerWeight, out float2 bestDir)
    {
        samples = math.max(samples, 1);
        float best = float.MinValue, worst = float.MaxValue;
        bestDir = float2.zero;
        float step = 2f * math.PI / samples;

        for (int k = 0; k < samples; k++)
        {
            math.sincos(phase + k * step, out float sa, out float ca);
            float2 d = new float2(ca, sa);
            float2 p = origin + d * radius;

            float v = 0f;
            if (_view.TryIndex(p, out int i))
                v = foodWeight * _view.At(ColonyChannel.Food, i) - dangerWeight * _view.At(ColonyChannel.Danger, i);

            if (v > best) { best = v; bestDir = d; }
            if (v < worst) worst = v;
        }
        return best - worst;
    }

    public void RaiseAlarm(float amount) => Alarm = math.min(1f, Alarm + math.max(0f, amount));

    public void RaiseHunger(float amount) => Hunger = math.min(1f, Hunger + math.max(0f, amount));

    public void Tick(float dt, int membersNearNest)
    {
        if (dt <= 0f) return;
        var r = SimulationRules.Active;

        for (int c = 0; c < ChannelCount; c++)
            _keep[c] = math.exp(-dt / math.max(r.ColonyChannelTau(c), 1e-3f));

        new ColonyFieldDecayJob
        {
            Field = _field,
            Keep  = _keep,
            Stats = _stats,
            Cells = _cells,
            Size  = _size,
        }.Run();

        int peak = (int)_stats[ColonyFieldStats.PeakIndex];
        HasFoodPeak = peak >= 0 && _stats[ColonyFieldStats.PeakValue] >= r.NestFoodPeakMin;
        if (HasFoodPeak)
            FoodPeak = _origin + (new float2(peak % _size, peak / _size) + 0.5f) * _cell;

        Alarm     = Alarm * math.exp(-dt / math.max(r.AlarmTau, 1e-3f));
        Hunger    = Hunger * math.exp(-dt / math.max(r.ColonyHungerTau, 1e-3f));
        AlarmHunt = membersNearNest >= r.AlarmHuntMinMembers;
        DangerAtNest = FieldAt(ColonyChannel.Danger, Position);

        UpdateMigration(dt, r);
    }

    private void UpdateMigration(float dt, SimulationRules r)
    {
        float2 toFood = float2.zero;
        if (_stats[ColonyFieldStats.FoodMass] > 1e-3f)
            toFood = math.normalizesafe(_origin + new float2(_stats[ColonyFieldStats.FoodX], _stats[ColonyFieldStats.FoodY]) * _cell - Position);

        float2 fromDanger = float2.zero;
        if (_stats[ColonyFieldStats.DangerMass] > 1e-3f)
            fromDanger = math.normalizesafe(Position - (_origin + new float2(_stats[ColonyFieldStats.DangerX], _stats[ColonyFieldStats.DangerY]) * _cell));

        float hunger = r.MigrationHungerWeight * Hunger;
        float danger = r.MigrationDangerWeight * math.saturate(DangerAtNest);
        float2 target = hunger * toFood + danger * fromDanger;

        float k = 1f - math.exp(-dt / math.max(r.MigrationTau, 1e-3f));
        Migration = math.lerp(Migration, target, k);
    }

    public void Relocate(float2 position)
    {
        float2 newOrigin = position - 0.5f * _size * _cell;
        int2   offset    = (int2)math.round((newOrigin - _origin) / _cell);
        if (math.all(offset == 0)) return;

        new ColonyFieldShiftJob
        {
            Source   = _field,
            Target   = _scratch,
            Cells    = _cells,
            Size     = _size,
            Channels = ChannelCount,
            Offset   = offset,
        }.Run();

        (_field, _scratch) = (_scratch, _field);
        _origin  = _origin + (float2)offset * _cell;
        Position = _origin + 0.5f * _size * _cell;
        Migration = float2.zero;
        Relocations++;
        RefreshView();
    }

    public void Dispose()
    {
        if (_field.IsCreated)   _field.Dispose();
        if (_scratch.IsCreated) _scratch.Dispose();
        if (_keep.IsCreated)    _keep.Dispose();
        if (_stats.IsCreated)   _stats.Dispose();
    }
}
