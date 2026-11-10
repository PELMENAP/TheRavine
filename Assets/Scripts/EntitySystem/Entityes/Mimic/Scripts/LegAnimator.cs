using UnityEngine;
using Unity.Mathematics;

public class LegAnimator
{
    public void Update(Mimic mimic, float dt)
    {
        float3 rootPos = mimic.transform.position;
        var legs = mimic.Pool.GetAll();

        for (int i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            if (leg.state == LegState.Disabled) continue;

            UpdateState(mimic, leg, dt);
            leg.progression = math.lerp(leg.progression, leg.growTarget, math.saturate(leg.growCoef * dt));
            UpdateHandles(leg, rootPos, dt);
        }
    }

    private void UpdateState(Mimic mimic, LegData leg, float dt)
    {
        switch (leg.state)
        {
            case LegState.Growing:
                if (leg.progression > 0.9f)
                {
                    leg.state = LegState.Supporting;
                    mimic.deployedLegs++;
                }
                break;
            case LegState.Supporting:
                leg.lifeTimer -= dt;
                float dx = mimic.legPlacerOrigin.x - leg.footPosition.x;
                float dz = mimic.legPlacerOrigin.z - leg.footPosition.z;
                bool tooFar = (dx * dx + dz * dz) > (mimic.maxLegDistance * mimic.maxLegDistance);
                bool timeUp = leg.lifeTimer <= 0;
                bool canRetract = mimic.deployedLegs > mimic.minimumAnchoredParts;

                if ((tooFar || timeUp) && canRetract)
                {
                    leg.state = LegState.Retracting;
                    leg.growTarget = 0f;
                    mimic.deployedLegs--;
                }
                break;
            case LegState.Retracting:
                if (leg.progression < 0.05f)
                {
                    leg.state = LegState.Disabled;
                    mimic.legCount--;
                }
                break;
        }
    }

    private void UpdateHandles(LegData leg, float3 rootPos, float dt)
    {
        float3[] h = leg.handles;
        float3[] o = leg.handleOffsets;

        h[0] = rootPos;
        h[6] = leg.footPosition + new float3(0f, 0.05f, 0f);

        h[2] = math.lerp(h[0], h[6], 0.4f);
        h[2].y = h[0].y + leg.legHeight;

        h[1] = math.lerp(h[0], h[2], 0.5f);
        h[3] = math.lerp(h[2], h[6], 0.25f);
        h[4] = math.lerp(h[2], h[6], 0.5f);
        h[5] = math.lerp(h[2], h[6], 0.75f);

        RotateHandleOffsets(leg, dt);

        h[1] += o[0];
        h[2] += o[1];
        h[3] += o[2];
        h[4] += o[3] * 0.5f;
        h[5] += o[4] * 0.25f;
    }

    private void RotateHandleOffsets(LegData leg, float dt)
    {
        leg.oscillationProgress += dt * leg.oscillationSpeed;
        if (leg.oscillationProgress >= 360f) leg.oscillationProgress -= 360f;

        float angle = leg.rotationSpeed * math.sin(math.radians(leg.oscillationProgress));
        math.sincos(math.radians(angle), out float sinTheta, out float cosTheta);
        float oneMinusCos = 1f - cosTheta;

        float3[] h = leg.handles;
        float3[] o = leg.handleOffsets;

        for (int i = 1; i < 6; i++)
        {
            float3 axis = h[i + 1] - h[i - 1];
            float l2 = math.lengthsq(axis);
            if (l2 <= 0.0001f) continue;

            axis *= math.rsqrt(l2);
            float3 v = o[i - 1];
            o[i - 1] = v * cosTheta + math.cross(axis, v) * sinTheta + axis * (math.dot(axis, v) * oneMinusCos);
        }
    }
}
