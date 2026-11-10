using UnityEngine;
using Unity.Mathematics;

public enum LegState { Disabled, Growing, Supporting, Retracting }

public class LegData
{
    public LegState state = LegState.Disabled;
    public float3 footPosition;
    public float3[] handles = new float3[7];
    public float3[] handleOffsets = new float3[7];
    public float progression;
    public float growTarget;
    public float lifeTimer;
    public float growCoef;
    public float legHeight;
    public float rotationSpeed;
    public float oscillationSpeed;
    public float oscillationProgress;

    public void Activate(Mimic mimic, Vector3 footPos, float coef, float lifeTime)
    {
        state = LegState.Growing;
        footPosition = footPos;
        progression = 0f;
        growTarget = 1f;
        growCoef = coef;
        lifeTimer = lifeTime;

        legHeight = RavineRandom.RangeFloat(mimic.legMinHeight, mimic.legMaxHeight);
        rotationSpeed = RavineRandom.RangeFloat(mimic.minRotSpeed, mimic.maxRotSpeed);
        oscillationSpeed = RavineRandom.RangeFloat(mimic.minOscillationSpeed, mimic.maxOscillationSpeed);
        oscillationProgress = 0f;

        for (int i = 0; i < 7; i++)
        {
            handleOffsets[i] = RavineRandom.UnitSphere() * RavineRandom.RangeFloat(mimic.handleOffsetMinRadius, mimic.handleOffsetMaxRadius);
        }
    }
}
