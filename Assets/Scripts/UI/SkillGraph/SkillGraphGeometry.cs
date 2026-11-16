using UnityEngine;

public static class SkillGraphGeometry
{
    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq <= 1e-12f) return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        return Vector2.Distance(point, a + ab * t);
    }

    public static int NearestEdge(SkillGraphSnapshot snapshot, System.Func<int, Vector2> toScreen, Vector2 point, float tolerance)
    {
        int best = -1;
        float bestDistance = tolerance;
        for (int e = 0; e < snapshot.EdgeCount; e++)
        {
            int from = snapshot.SlotOf(snapshot.EdgeFrom[e]);
            int to = snapshot.SlotOf(snapshot.EdgeTo[e]);
            if (from < 0 || to < 0) continue;
            float d = DistanceToSegment(point, toScreen(from), toScreen(to));
            if (d > bestDistance) continue;
            bestDistance = d;
            best = e;
        }
        return best;
    }
}
