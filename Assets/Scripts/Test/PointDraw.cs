using System;
using System.Collections.Generic;
using ZLinq;
using UnityEngine;

public class PointPathVisualizer : MonoBehaviour
{
    public List<Vector2> points = new List<Vector2>
    {
        new Vector2(0, 0),
    };

    private void Start()
    {
        for(int i = 0; i < 200; i++)
        {
            points.Add(new Vector2(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)));
        }

        List<Vector2> path = BuildPath(points);
        VisualizePath(path);
    }

    public static List<Vector2> BuildPath(List<Vector2> points)
    {
        if (points.Count <= 2)
            return points;

        List<Vector2> hull = ConvexHull(points);
        List<Vector2> remaining = points.AsValueEnumerable().Except(hull).ToList();

        return InsertPointsGreedy(hull, remaining);
    }

    private static List<Vector2> InsertPointsGreedy(List<Vector2> path, List<Vector2> remaining)
    {
        foreach (var p in remaining)
        {
            int bestIndex = 0;
            float bestIncrease = float.MaxValue;

            for (int i = 0; i < path.Count; i++)
            {
                int nextIndex = (i + 1) % path.Count;
                float increase = (path[i] - p).magnitude + (p - path[nextIndex]).magnitude - (path[i] - path[nextIndex]).magnitude;
                
                if (increase < bestIncrease)
                {
                    bestIncrease = increase;
                    bestIndex = nextIndex;
                }
            }

            path.Insert(bestIndex, p);
        }

        return path;
    }

    public static List<Vector2> ConvexHull(List<Vector2> points)
    {
        if (points.Count <= 3)
            return points.AsValueEnumerable().Distinct().ToList();

        points = points.AsValueEnumerable().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        List<Vector2> hull = new List<Vector2>();

        foreach (var point in points)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point))
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }

        int lowerHullCount = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerHullCount && Cross(hull[^2], hull[^1], points[i]))
                hull.RemoveAt(hull.Count - 1);
            hull.Add(points[i]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static bool Cross(Vector2 O, Vector2 A, Vector2 B) => ((A.x - O.x) * (B.y - O.y) - (A.y - O.y) * (B.x - O.x)) <= 0;

    private void VisualizePath(List<Vector2> sortedPoints)
    {
        for (int i = 0; i < sortedPoints.Count; i++)
        {
            Vector2 start = sortedPoints[i];
            Vector2 end = sortedPoints[(i + 1) % sortedPoints.Count];

            Debug.DrawLine(start, end, Color.green, 10f);
        }
    }
}
