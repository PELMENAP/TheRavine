using UnityEngine;

public class LegAnimator
{
    public void Update(Mimic mimic, float dt)
    {
        Vector3 rootPos = mimic.transform.position;
        
        foreach (var leg in mimic.Pool.GetAll())
        {
            if (leg.state == LegState.Disabled) continue;

            UpdateState(mimic, leg, dt);
            leg.progression = Mathf.Lerp(leg.progression, leg.growTarget, leg.growCoef * dt);
            UpdateHandles(mimic, leg, rootPos, dt);
            UpdateBezier(leg);
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

    private void UpdateHandles(Mimic mimic, LegData leg, Vector3 rootPos, float dt)
    {
        leg.handles[0] = rootPos;
        leg.handles[6] = leg.footPosition + new Vector3(0f, 0.05f, 0f);

        leg.handles[2] = Vector3.Lerp(leg.handles[0], leg.handles[6], 0.4f);
        leg.handles[2].y = leg.handles[0].y + leg.legHeight;

        leg.handles[1] = Vector3.Lerp(leg.handles[0], leg.handles[2], 0.5f);
        leg.handles[3] = Vector3.Lerp(leg.handles[2], leg.handles[6], 0.25f);
        leg.handles[4] = Vector3.Lerp(leg.handles[2], leg.handles[6], 0.5f);
        leg.handles[5] = Vector3.Lerp(leg.handles[2], leg.handles[6], 0.75f);

        RotateHandleOffsets(leg, dt);

        leg.handles[1] += leg.handleOffsets[0];
        leg.handles[2] += leg.handleOffsets[1];
        leg.handles[3] += leg.handleOffsets[2];
        leg.handles[4] += leg.handleOffsets[3] * 0.5f;
        leg.handles[5] += leg.handleOffsets[4] * 0.25f;
    }

    private void RotateHandleOffsets(LegData leg, float dt)
    {
        leg.oscillationProgress += dt * leg.oscillationSpeed;
        if (leg.oscillationProgress >= 360f) leg.oscillationProgress -= 360f;

        float angle = leg.rotationSpeed * Mathf.Sin(leg.oscillationProgress * Mathf.Deg2Rad);
        float cosTheta = Mathf.Cos(angle * Mathf.Deg2Rad);
        float sinTheta = Mathf.Sin(angle * Mathf.Deg2Rad);
        float oneMinusCos = 1f - cosTheta;

        for (int i = 1; i < 6; i++)
        {
            Vector3 axis = leg.handles[i + 1] - leg.handles[i - 1];
            if (axis.sqrMagnitude > 0.0001f)
            {
                axis.Normalize();
                Vector3 v = leg.handleOffsets[i - 1];
                Vector3 cross = Vector3.Cross(axis, v);
                float dot = Vector3.Dot(axis, v);
                leg.handleOffsets[i - 1] = v * cosTheta + cross * sinTheta + axis * (dot * oneMinusCos);
            }
        }
    }

    private void UpdateBezier(LegData leg)
    {
        float step = 1f / (leg.resolution - 1);
        Vector3[] pts = leg.handles;
        
        for (int i = 0; i < leg.resolution; i++)
        {
            float t = i * step * leg.progression;
            if (leg.progression < 0.0001f) t = 0;
            
            float u = 1f - t;
            float u2 = u * u;
            float u3 = u2 * u;
            float u4 = u3 * u;
            float u5 = u4 * u;
            float u6 = u5 * u;
            float t2 = t * t;
            float t3 = t2 * t;
            float t4 = t3 * t;
            float t5 = t4 * t;
            float t6 = t5 * t;

            leg.sampleBuffer[i] = pts[0] * u6 +
                                  6f * t * u5 * pts[1] +
                                  15f * t2 * u4 * pts[2] +
                                  20f * t3 * u3 * pts[3] +
                                  15f * t4 * u2 * pts[4] +
                                  6f * t5 * u * pts[5] +
                                  pts[6] * t6;
        }
    }
}