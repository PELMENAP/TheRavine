using Unity.Mathematics;

public struct ColonyPoi
{
    public float2 Position;
    public float  EnergyEma;
    public float  Danger;
    public double Updated;
}

public sealed class ColonyPoiBoard
{
    private readonly ColonyPoi[] _points;
    private int _count;

    public int Count => _count;
    public ref readonly ColonyPoi At(int i) => ref _points[i];

    public ColonyPoiBoard(int capacity) => _points = new ColonyPoi[math.max(capacity, 1)];

    public void Offer(float2 position, float energy, float danger)
    {
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        float merge2 = r.ColonyPoiMergeRadius * r.ColonyPoiMergeRadius;

        for (int i = 0; i < _count; i++)
        {
            ref var p = ref _points[i];
            if (math.distancesq(p.Position, position) > merge2) continue;
            float a = r.PoiEnergyEmaAlpha;
            p.EnergyEma = math.lerp(p.EnergyEma, energy, a);
            p.Danger    = math.max(DecayedDanger(in p, now, in r), danger);
            p.Updated   = now;
            return;
        }

        var poi = new ColonyPoi { Position = position, EnergyEma = energy, Danger = danger, Updated = now };
        if (_count < _points.Length)
        {
            _points[_count++] = poi;
            return;
        }

        int worst = 0;
        float worstScore = float.MaxValue;
        for (int i = 0; i < _count; i++)
        {
            float v = Value(in _points[i], now, in r);
            if (v >= worstScore) continue;
            worstScore = v;
            worst = i;
        }
        if (Value(in poi, now, in r) > worstScore) _points[worst] = poi;
    }

    public bool TryPickBest(float2 self, out float2 target, out float score)
    {
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        score  = float.MinValue;
        target = default;
        int pick = -1;

        for (int i = 0; i < _count; i++)
        {
            ref readonly var p = ref _points[i];
            float v = Value(in p, now, in r) - math.distance(p.Position, self) * r.PoiDistanceCost;
            if (v <= score) continue;
            score = v;
            pick  = i;
        }

        if (pick < 0) return false;
        target = _points[pick].Position;
        return true;
    }

    public void CopyTo(ColonyPoiBoard other)
    {
        for (int i = 0; i < _count; i++)
        {
            ref readonly var p = ref _points[i];
            other.Offer(p.Position, p.EnergyEma, p.Danger);
        }
    }

    private static float DecayedDanger(in ColonyPoi p, double now, in SimulationRules.RulesFrame r)
        => p.Danger * math.exp(-(float)(now - p.Updated) / math.max(r.ColonyPoiDangerTau, 1e-3f));

    private static float Value(in ColonyPoi p, double now, in SimulationRules.RulesFrame r)
        => p.EnergyEma - r.ColonyPoiDangerWeight * DecayedDanger(in p, now, in r);
}
