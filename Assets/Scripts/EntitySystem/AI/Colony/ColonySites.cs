using System;
using Unity.Mathematics;

public static class ColonySites
{
    public const float Unsuitable = -1f;

    public static int Choose(float2[] sites, int existing, int count, float2 centre, float radius, float spacing, int candidates, Func<float2, float> score)
    {
        int placed = 0;
        float spacingSq = spacing * spacing;
        for (int k = 0; k < count && existing + placed < sites.Length; k++)
        {
            int taken = existing + placed;
            bool hasBest = false, hasUnknown = false;
            float best = 0f;
            float2 bestSite = default, unknownSite = default;
            for (int i = 0; i < candidates; i++)
            {
                var v = RavineRandom.GetInsideCircle(radius);
                float2 p = centre + new float2(v.x, v.y);
                if (TooClose(sites, taken, p, spacingSq)) continue;

                float s = score(p);
                if (s < 0f) continue;
                if (s == 0f)
                {
                    if (hasUnknown) continue;
                    hasUnknown = true;
                    unknownSite = p;
                    continue;
                }
                if (hasBest && s <= best) continue;
                hasBest = true;
                best = s;
                bestSite = p;
            }
            if (!hasBest && !hasUnknown) break;
            sites[taken] = hasBest ? bestSite : unknownSite;
            placed++;
        }
        return placed;
    }

    private static bool TooClose(float2[] sites, int count, float2 p, float spacingSq)
    {
        for (int i = 0; i < count; i++)
            if (math.distancesq(sites[i], p) < spacingSq) return true;
        return false;
    }
}
