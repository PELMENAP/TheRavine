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
        if (points.Count > 0 && math.distancesq(points[0], pos) < minDistance * minDistance)
            return false;

        if (points.Count >= MaxPoints)
            points.RemoveAt(0);

        points.Add(pos);
        return true;
    }

    public float2 GetRandom() => points[RavineRandom.RangeInt(0, points.Count)];

    public void Dispose() { }
}