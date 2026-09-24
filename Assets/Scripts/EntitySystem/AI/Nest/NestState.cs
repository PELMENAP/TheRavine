using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public enum ColonyChannel : byte
{
    Food   = 0,
    Danger = 1,
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

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct ColonyFieldDecayJob : IJob
{
    public NativeArray<float> Field;
    [ReadOnly] public NativeArray<float> Keep;
    [WriteOnly] public NativeArray<float> Peak;
    public int Cells;
    public int PeakChannel;

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
        int   po      = PeakChannel * Cells;
        for (int i = 0; i < Cells; i++)
        {
            float f = Field[po + i];
            if (f > bestVal) { bestVal = f; best = i; }
        }
        Peak[0] = best;
        Peak[1] = bestVal;
    }
}

public sealed unsafe class NestState : IDisposable
{
    public const int ChannelCount = (int)ColonyChannel.Count;

    public float2 Position { get; private set; }
    public float  Radius   { get; private set; }

    public float Storage;
    public float Alarm { get; private set; }
    public bool  AlarmHunt { get; private set; }

    public float2 FoodPeak { get; private set; }
    public bool   HasFoodPeak { get; private set; }

    private NativeArray<float> _field;
    private NativeArray<float> _keep;
    private NativeArray<float> _peak;
    private readonly ColonyFieldView _view;
    private readonly int   _size;
    private readonly int   _cells;
    private readonly float _cell;
    private readonly float2 _origin;

    public NestState(float2 position)
    {
        var r = SimulationRules.Active;
        Position = position;
        Radius   = r.NestRadius;
        _size    = math.max(4, r.NestMapSize);
        _cells   = _size * _size;
        _cell    = math.max(0.5f, r.NestMapCellSize);
        _origin  = position - 0.5f * _size * _cell;

        _field = new NativeArray<float>(_cells * ChannelCount, Allocator.Persistent);
        _keep  = new NativeArray<float>(ChannelCount, Allocator.Persistent);
        _peak  = new NativeArray<float>(2, Allocator.Persistent);

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

    public void RaiseAlarm(float amount) => Alarm = math.min(1f, Alarm + math.max(0f, amount));

    public void Tick(float dt, int membersNearNest)
    {
        if (dt <= 0f) return;
        var r = SimulationRules.Active;

        for (int c = 0; c < ChannelCount; c++)
            _keep[c] = math.exp(-dt / math.max(r.ColonyChannelTau(c), 1e-3f));

        new ColonyFieldDecayJob
        {
            Field       = _field,
            Keep        = _keep,
            Peak        = _peak,
            Cells       = _cells,
            PeakChannel = (int)ColonyChannel.Food,
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
        if (_field.IsCreated) _field.Dispose();
        if (_keep.IsCreated)  _keep.Dispose();
        if (_peak.IsCreated)  _peak.Dispose();
    }
}
