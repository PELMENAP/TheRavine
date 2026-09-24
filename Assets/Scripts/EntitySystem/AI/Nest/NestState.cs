using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct NestMapView
{
    [ReadOnly] public NativeArray<float> Food;
    [ReadOnly] public NativeArray<float> Danger;
    public float2 Origin;
    public float  InvCell;
    public int    Size;

    public bool TryIndex(float2 p, out int index)
    {
        int2 c = (int2)math.floor((p - Origin) * InvCell);
        if (math.any(c < 0) || math.any(c >= Size)) { index = -1; return false; }
        index = c.y * Size + c.x;
        return true;
    }

    public float FoodAt(float2 p)   => TryIndex(p, out int i) ? Food[i] : 0f;
    public float DangerAt(float2 p) => TryIndex(p, out int i) ? Danger[i] : 0f;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct NestDecayJob : IJob
{
    public NativeArray<float> Food;
    public NativeArray<float> Danger;
    public NativeArray<float> Peak;
    public float FoodKeep;
    public float DangerKeep;

    public void Execute()
    {
        int best = -1;
        float bestVal = 0f;
        for (int i = 0; i < Food.Length; i++)
        {
            float f = Food[i] * FoodKeep;
            Food[i]   = f;
            Danger[i] = Danger[i] * DangerKeep;
            if (f > bestVal) { bestVal = f; best = i; }
        }
        Peak[0] = best;
        Peak[1] = bestVal;
    }
}

public sealed class NestState : IDisposable
{
    public float2 Position { get; private set; }
    public float  Radius   { get; private set; }

    public float Storage;
    public float Alarm { get; private set; }
    public bool  AlarmHunt { get; private set; }

    public float2 FoodPeak { get; private set; }
    public bool   HasFoodPeak { get; private set; }

    private NativeArray<float> _food;
    private NativeArray<float> _danger;
    private NativeArray<float> _peak;
    private readonly int   _size;
    private readonly float _cell;
    private readonly float2 _origin;

    public NestState(float2 position)
    {
        var r = SimulationRules.Active;
        Position = position;
        Radius   = r.NestRadius;
        _size    = math.max(4, r.NestMapSize);
        _cell    = math.max(0.5f, r.NestMapCellSize);
        _origin  = position - 0.5f * _size * _cell;

        _food   = new NativeArray<float>(_size * _size, Allocator.Persistent);
        _danger = new NativeArray<float>(_size * _size, Allocator.Persistent);
        _peak   = new NativeArray<float>(2, Allocator.Persistent);
    }

    public NestMapView View => new NestMapView
    {
        Food = _food, Danger = _danger, Origin = _origin, InvCell = 1f / _cell, Size = _size,
    };

    public bool Contains(float2 p) => math.distancesq(p, Position) <= Radius * Radius;

    public void MarkFood(float2 p, float amount)
    {
        if (View.TryIndex(p, out int i)) _food[i] = math.min(_food[i] + amount, SimulationRules.Frame.NestMapMaxValue);
    }

    public void MarkDanger(float2 p, float amount)
    {
        if (View.TryIndex(p, out int i)) _danger[i] = math.min(_danger[i] + amount, SimulationRules.Frame.NestMapMaxValue);
    }

    public float DangerAt(float2 p) => View.DangerAt(p);

    public void RaiseAlarm(float amount) => Alarm = math.min(1f, Alarm + math.max(0f, amount));

    public void Tick(float dt, int membersNearNest)
    {
        if (dt <= 0f) return;
        var r = SimulationRules.Active;

        new NestDecayJob
        {
            Food       = _food,
            Danger     = _danger,
            Peak       = _peak,
            FoodKeep   = math.exp(-dt / math.max(r.NestFoodTau, 1e-3f)),
            DangerKeep = math.exp(-dt / math.max(r.NestDangerTau, 1e-3f)),
        }.Run();

        int peak = (int)_peak[0];
        HasFoodPeak = peak >= 0 && _peak[1] >= r.NestFoodPeakMin;
        if (HasFoodPeak)
            FoodPeak = _origin + (new float2(peak % _size, peak / _size) + 0.5f) * _cell;

        Alarm     = Alarm * math.exp(-dt / math.max(r.AlarmTau, 1e-3f));
        AlarmHunt = membersNearNest >= r.AlarmHuntMinMembers;
    }

    public void Dispose()
    {
        if (_food.IsCreated)   _food.Dispose();
        if (_danger.IsCreated) _danger.Dispose();
        if (_peak.IsCreated)   _peak.Dispose();
    }
}
