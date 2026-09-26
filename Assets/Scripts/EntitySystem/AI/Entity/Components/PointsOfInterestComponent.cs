using Unity.Mathematics;

public struct PointOfInterest
{
    public float2 Position;
    public float  EnergyEma;
    public double LastVisit;
}

public struct NegativePoint
{
    public float2 Position;
    public float  Strength;
    public double Time;
}

public class PointsOfInterestComponent : IComponent
{
    private const int MaxPoints = 5;
    private readonly PointOfInterest[] _points = new PointOfInterest[MaxPoints];
    private int _count;

    private readonly NegativePoint[] _negative = new NegativePoint[System.Math.Max(1, SimulationRules.Active.NegativePoiCount)];
    private int _negativeCount;

    public void MarkNegative(in float2 pos, float strength)
    {
        if (strength <= 0f) return;
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        float rad2 = r.NegativePoiRadius * r.NegativePoiRadius;

        int weakest = 0;
        float weakestValue = float.MaxValue;
        for (int i = 0; i < _negativeCount; i++)
        {
            ref var n = ref _negative[i];
            float v = Decayed(in n, now, in r);
            if (math.distancesq(n.Position, pos) <= rad2)
            {
                n.Strength = v + strength;
                n.Time     = now;
                return;
            }
            if (v < weakestValue) { weakestValue = v; weakest = i; }
        }

        var point = new NegativePoint { Position = pos, Strength = strength, Time = now };
        if (_negativeCount < _negative.Length) _negative[_negativeCount++] = point;
        else if (weakestValue < strength) _negative[weakest] = point;
    }

    public float NegativeAt(in float2 pos)
    {
        if (_negativeCount == 0) return 0f;
        ref readonly var r = ref SimulationRules.Frame;
        double now = SimulationClock.TimeD;
        float rad2 = r.NegativePoiRadius * r.NegativePoiRadius;
        float sum = 0f;
        for (int i = 0; i < _negativeCount; i++)
        {
            ref readonly var n = ref _negative[i];
            if (math.distancesq(n.Position, pos) <= rad2) sum += Decayed(in n, now, in r);
        }
        return sum;
    }

    private static float Decayed(in NegativePoint n, double now, in SimulationRules.RulesFrame r)
        => n.Strength * math.exp(-(float)(now - n.Time) / math.max(r.NegativePoiTau, 1e-3f));

    public int Count => _count;
    public float2 Get(int idx) => _points[idx].Position;
    public ref readonly PointOfInterest At(int idx) => ref _points[idx];

    public bool TryRemember(in float2 pos, float minDistance)
    {
        float min2 = minDistance * minDistance;
        for (int i = 0; i < _count; i++)
            if (math.distancesq(_points[i].Position, pos) < min2) return false;

        if (_count >= MaxPoints)
        {
            int worst = 0;
            for (int i = 1; i < _count; i++)
                if (Weaker(in _points[i], in _points[worst])) worst = i;
            _points[worst] = _points[--_count];
        }

        _points[_count++] = new PointOfInterest { Position = pos, EnergyEma = 0f, LastVisit = SimulationClock.TimeD };
        return true;
    }

    private static bool Weaker(in PointOfInterest a, in PointOfInterest b)
        => a.EnergyEma < b.EnergyEma || (a.EnergyEma == b.EnergyEma && a.LastVisit < b.LastVisit);

    public bool TryGetNearest(in float2 pos, out float2 nearest)
    {
        int i = NearestIndex(in pos, float.MaxValue);
        nearest = i >= 0 ? _points[i].Position : default;
        return i >= 0;
    }

    public bool TryPickBest(in float2 self, out float2 target)
    {
        ref readonly var r = ref SimulationRules.Frame;
        double now  = SimulationClock.TimeD;
        float  best = float.MinValue;
        int    pick = -1;

        for (int i = 0; i < _count; i++)
        {
            ref readonly var p = ref _points[i];
            float since   = (float)(now - p.LastVisit);
            float novelty = r.PoiRevisitWeight * math.saturate(since / math.max(r.PoiRevisitTau, 1e-3f));
            float score   = p.EnergyEma + novelty - math.distance(p.Position, self) * r.PoiDistanceCost
                          - r.NegativePoiWeight * NegativeAt(in p.Position);
            if (score <= best) continue;
            best = score;
            pick = i;
        }

        target = pick >= 0 ? _points[pick].Position : default;
        return pick >= 0;
    }

    public void Visit(in float2 pos)
    {
        int i = NearestIndex(in pos, SimulationRules.Frame.PoiVisitRadius);
        if (i >= 0) _points[i].LastVisit = SimulationClock.TimeD;
    }

    public void CreditEnergy(in float2 pos, float energy)
    {
        ref readonly var r = ref SimulationRules.Frame;
        int i = NearestIndex(in pos, r.PoiVisitRadius);
        if (i < 0) return;
        ref var p = ref _points[i];
        p.EnergyEma = math.lerp(p.EnergyEma, energy, r.PoiEnergyEmaAlpha);
        p.LastVisit = SimulationClock.TimeD;
    }

    private int NearestIndex(in float2 pos, float radius)
    {
        float best = radius < float.MaxValue ? radius * radius : float.MaxValue;
        int   pick = -1;
        for (int i = 0; i < _count; i++)
        {
            float d = math.distancesq(_points[i].Position, pos);
            if (d >= best) continue;
            best = d;
            pick = i;
        }
        return pick;
    }

    public void Dispose() { }
}
