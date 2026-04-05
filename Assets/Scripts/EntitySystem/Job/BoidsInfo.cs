using UnityEngine;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "BoidsInfo", menuName = "Visual/Create New BoidsInfo")]
public class BoidsInfo : ScriptableObject
{
    public int numberOfEntities, delayFactor, nearTheTarget, distanceOfTargetFromPlayer;
    public float destinationThreshold, avoidanceThreshold, alongThreshold, cellSize, velocityLimit;
    public float3 accelerationWeights;
    public Quaternion flip;

    [Header("3D Y Movement")]
    public float yTarget;
    public float ySpawnSpread = 1f;
}
