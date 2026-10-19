using UnityEngine;
using UnityEngine.Serialization;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "BoidsInfo", menuName = "Visual/Create New BoidsInfo")]
public class BoidsInfo : ScriptableObject
{
    public int numberOfEntities, nearTheTarget, distanceOfTargetFromPlayer;
    public float avoidanceThreshold, alongThreshold, velocityLimit;
    public float3 accelerationWeights;

    [Header("Target")]
    [FormerlySerializedAs("destinationThreshold")] public float slowRadius = 40f;
    public float targetWeight = 1f;
    public float2 flockRetargetInterval = new(3f, 30f);

    [Header("Time")]
    public float maxDeltaTime = 0.1f;

    [Header("View")]
    public float flipYaw = 180f;

    [Header("3D Y Movement")]
    public float yTarget;
    public float ySpawnSpread = 1f;

    public float CellSize => math.max(avoidanceThreshold, alongThreshold);

    private void OnValidate()
    {
        numberOfEntities = math.max(numberOfEntities, 0);
        avoidanceThreshold = math.max(avoidanceThreshold, 0.01f);
        alongThreshold = math.max(alongThreshold, avoidanceThreshold);
        slowRadius = math.max(slowRadius, 0.01f);
        velocityLimit = math.max(velocityLimit, 0.01f);
        maxDeltaTime = math.max(maxDeltaTime, 0.001f);
        flockRetargetInterval.x = math.max(flockRetargetInterval.x, 0.01f);
        flockRetargetInterval.y = math.max(flockRetargetInterval.y, flockRetargetInterval.x);
    }
}
