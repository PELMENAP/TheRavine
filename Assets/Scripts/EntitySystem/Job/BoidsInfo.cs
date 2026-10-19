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

    [Header("Flocking 3D")]
    public float verticalFlockScale = 0.3f;
    public float minAirSpeed = 1f;

    [Header("Time")]
    public float maxDeltaTime = 0.1f;

    [Header("View")]
    public float flipYaw = 180f;
    public float flipDeadzone = 0.1f;

    [Header("Spawn")]
    public float ySpawnSpread = 1f;

    [Header("Height Field")]
    public int heightFieldSize = 128;
    public int heightCellStep = 2;
    [Range(0.05f, 1f)] public float recenterFraction = 0.5f;
    [FormerlySerializedAs("yTarget")] public float cruiseAGL = 20f;
    public float heightLookahead = 1.5f;
    public float heightKp = 2f, heightKd = 1.5f;
    public float minClearance = 2f;

    [Header("Trees")]
    public float perchCellSize = 8f;
    public float treeMargin = 2f;
    public float treeAvoidWeight = 30f;

    [Header("Perch Decision")]
    [Range(0f, 1f)] public float flockPerchChance = 0.35f;
    [Range(0f, 1f)] public float groundPerchChance = 0.4f;
    public float maxPerchSlope = 25f;
    public float waterThreshold = 0.1f;
    public float perchSearchRadius = 60f;
    public float groundPerchSpread = 6f;
    public int groundPerchTries = 4;
    public float groundPerchOffset = 0.2f;

    [Header("Tree Slots")]
    [Range(0f, 1f)] public float perchMinHeight = 0.4f;
    [Range(0f, 1f)] public float perchRadiusMin = 0.7f;
    public int perchSlotsTheta = 8, perchSlotsHeight = 8;

    [Header("Approach / Landing")]
    public float approachSlowRadius = 25f;
    public float approachFlockFade = 20f;
    public float approachTimeout = 20f;
    public float landingStartDistance = 8f;
    public float landingApproachHeight = 4f;
    public float landingSpeed = 6f;
    public float2 landingDuration = new(0.6f, 2.5f);
    public float landingFinalSpeed = 1.5f;
    public float landingDescent = 0.5f;
    public float landingWindBias = 2f;

    [Header("Perched")]
    public float2 perchDuration = new(8f, 30f);
    public float2 hopInterval = new(2f, 6f);
    public float hopDuration = 0.25f, hopHeight = 0.4f, hopDistance = 0.5f;
    public float swayAmplitude = 0.3f;

    [Header("Scare / Take Off")]
    public float scareRadius = 12f;
    public float panicRadius = 5f;
    public float panicDelay = 0.25f;
    public float takeOffUp = 8f, takeOffOut = 6f, takeOffDuration = 1f;

    [Header("Wind")]
    public float windDrift = 4f;

    [Header("Animation")]
    [Range(0f, 1f)] public float flapMinScale = 0.5f;

    public float CellSize => math.max(avoidanceThreshold, alongThreshold);
    public int SlotCount => perchSlotsTheta * perchSlotsHeight;

    private void OnValidate()
    {
        numberOfEntities = math.max(numberOfEntities, 0);
        avoidanceThreshold = math.max(avoidanceThreshold, 0.01f);
        alongThreshold = math.max(alongThreshold, avoidanceThreshold);
        slowRadius = math.max(slowRadius, 0.01f);
        velocityLimit = math.max(velocityLimit, 0.01f);
        minAirSpeed = math.clamp(minAirSpeed, 0f, velocityLimit);
        maxDeltaTime = math.max(maxDeltaTime, 0.001f);
        flockRetargetInterval.x = math.max(flockRetargetInterval.x, 0.01f);
        flockRetargetInterval.y = math.max(flockRetargetInterval.y, flockRetargetInterval.x);

        heightFieldSize = math.max(heightFieldSize, 4);
        heightCellStep = math.max(heightCellStep, 1);
        heightKp = math.max(heightKp, 0f);
        heightKd = math.max(heightKd, 0f);

        perchCellSize = math.max(perchCellSize, 0.5f);
        treeMargin = math.max(treeMargin, 0f);
        groundPerchTries = math.max(groundPerchTries, 1);
        maxPerchSlope = math.clamp(maxPerchSlope, 0f, 89f);

        perchSlotsTheta = math.clamp(perchSlotsTheta, 1, 64);
        perchSlotsHeight = math.clamp(perchSlotsHeight, 1, 64 / perchSlotsTheta);

        approachSlowRadius = math.max(approachSlowRadius, 0.01f);
        approachFlockFade = math.max(approachFlockFade, 0.01f);
        landingSpeed = math.max(landingSpeed, 0.01f);
        landingDuration.x = math.max(landingDuration.x, 0.05f);
        landingDuration.y = math.max(landingDuration.y, landingDuration.x);
        perchDuration.x = math.max(perchDuration.x, 0f);
        perchDuration.y = math.max(perchDuration.y, perchDuration.x);
        hopInterval.x = math.max(hopInterval.x, 0.05f);
        hopInterval.y = math.max(hopInterval.y, hopInterval.x);
        hopDuration = math.max(hopDuration, 0.01f);
        takeOffDuration = math.max(takeOffDuration, 0.01f);

        panicRadius = math.clamp(panicRadius, 0f, CellSize);
    }
}
