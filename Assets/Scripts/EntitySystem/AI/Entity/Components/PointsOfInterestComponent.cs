using System.Collections.Generic;
using UnityEngine;

public class PointsOfInterestComponent : IComponent
{
    private const int MaxPoints = 5;
    private readonly List<Vector2> points = new(MaxPoints);

    public int Count => points.Count;
    public Vector2 Get(int idx) => points[idx];

    public bool TryRemember(Vector2 pos, float minDistance)
    {
        if (points.Count > 0 && Vector2.Distance(points[0], pos) < minDistance)
            return false;

        if (points.Count >= MaxPoints)
            points.RemoveAt(0);

        points.Add(pos);
        return true;
    }

    public Vector2 GetRandom() => points[RavineRandom.RangeInt(0, points.Count)];

    public void Dispose() { }
}