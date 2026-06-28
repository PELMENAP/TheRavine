using UnityEngine;

public enum LegState { Disabled, Growing, Supporting, Retracting }

public class LegData
{
    public LegState state = LegState.Disabled;
    public Vector3 footPosition;
    public Vector3[] handles = new Vector3[7];
    public Vector3[] handleOffsets = new Vector3[7];
    public Vector3[] sampleBuffer;
    public int resolution;
    public float progression;
    public float growTarget;
    public float lifeTimer;
    public float growCoef;
    public float legHeight;
    public float rotationSpeed;
    public float oscillationSpeed;
    public float oscillationProgress;

    public void Initialize(int res)
    {
        resolution = res;
        sampleBuffer = new Vector3[resolution];
    }

    public void Activate(Mimic mimic, Vector3 footPos, float coef, float lifeTime)
    {
        state = LegState.Growing;
        footPosition = footPos;
        progression = 0f;
        growTarget = 1f;
        growCoef = coef;
        lifeTimer = lifeTime;

        legHeight = Random.Range(mimic.legMinHeight, mimic.legMaxHeight);
        rotationSpeed = Random.Range(mimic.minRotSpeed, mimic.maxRotSpeed);
        oscillationSpeed = Random.Range(mimic.minOscillationSpeed, mimic.maxOscillationSpeed);
        oscillationProgress = 0f;

        for (int i = 0; i < 7; i++)
        {
            handleOffsets[i] = Random.onUnitSphere * Random.Range(mimic.handleOffsetMinRadius, mimic.handleOffsetMaxRadius);
        }
    }
}