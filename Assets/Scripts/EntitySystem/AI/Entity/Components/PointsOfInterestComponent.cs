using System.Collections.Generic;
using Unity.Mathematics;

public class PointsOfInterestComponent : IComponent
{
    private const int MaxPoints = 5;
    private readonly List<float2> points = new(MaxPoints);

    public int Count => points.Count;
    public float2 Get(int idx) => points[idx];

    public bool TryRemember(in float2 pos, float minDistance)
    {
        float min2 = minDistance * minDistance;
        for (int i = 0; i < points.Count; i++)
            if (math.distancesq(points[i], pos) < min2) return false;

        if (points.Count >= MaxPoints)
            points.RemoveAt(0);

        points.Add(pos);
        return true;
    }

    public bool TryGetNearest(in float2 pos, out float2 nearest)
    {
        nearest = default;
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = math.distancesq(points[i], pos);
            if (d >= best) continue;
            best    = d;
            nearest = points[i];
        }
        return best < float.MaxValue;
    }

    public float2 GetRandom() => points[RavineRandom.RangeInt(0, points.Count)];

    public void Dispose() { }
}