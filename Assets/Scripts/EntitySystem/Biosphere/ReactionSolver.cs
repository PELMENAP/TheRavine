using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Biosphere
{
    public static class ReactionSolver
    {
        public static void Step(ChemistryTable table, Span<float> pool, ref float energy, ReadOnlySpan<float> activity,
            float rate, float dt, float efficiency, out float loss)
        {
            loss = 0f;
            if (!(dt > 0f) || !(rate > 0f)) return;
            int n = math.min(table.ReactionCount, activity.Length);
            for (int k = 0; k < n; k++)
            {
                float a = math.saturate(activity[k]);
                if (!(a > 0f)) continue;
                int4 r = table.Reaction(k);
                float available = pool[r.x];
                if (r.y >= 0) available = r.y == r.x ? available * 0.5f : math.min(available, pool[r.y]);
                float extent = math.min(a * rate * dt * available, available);
                if (!(extent > 0f)) continue;

                float delta = table.EnergyDelta(k);
                if (delta > 0f)
                {
                    float affordable = math.max(energy, 0f) / delta;
                    extent = math.min(extent, affordable);
                    if (!(extent > 0f)) continue;
                    energy -= extent * delta;
                }
                else if (delta < 0f)
                {
                    float released = -delta * extent;
                    float kept = released * math.saturate(efficiency);
                    energy += kept;
                    loss += released - kept;
                }

                pool[r.x] -= extent;
                if (r.y >= 0) pool[r.y] -= extent;
                pool[r.z] += extent;
                if (r.w >= 0) pool[r.w] += extent;
                pool[r.x] = math.max(pool[r.x], 0f);
                if (r.y >= 0) pool[r.y] = math.max(pool[r.y], 0f);
            }
        }
    }
}
