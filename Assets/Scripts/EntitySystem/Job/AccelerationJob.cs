using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct AccelerationJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Vector3> Positions;
    [ReadOnly]
    public NativeArray<Vector3> OtherTargets;
    [ReadOnly]
    public NativeArray<Vector3> Velocities;

    public NativeArray<Vector3> Accelerations;

    public float DestinationThreshold;
    public Vector3 Weights;

    private int Count => Positions.Length - 1;

    public void Execute(int index)
    {
        Vector3 averageSpread = Vector3.zero,
            averageVelocity = Vector3.zero,
            averagePosition = Vector3.zero;

        for (int i = 0; i < Count; i++)
        {
            if (i == index || i % OtherTargets.Length != 0)
                continue;
            var targetPos = Positions[i];
            var posDifference = Positions[index] - targetPos;
            if (posDifference.magnitude > DestinationThreshold)
                continue;
            if (posDifference.magnitude < 5f)
            {
                averageSpread -= posDifference.normalized;
                averageVelocity -= Velocities[i];
                averagePosition -= targetPos;
            }
            averageSpread += posDifference.normalized;
            averageVelocity += Velocities[i];
            averagePosition += targetPos;
        }

        for (int i = 0; i < OtherTargets.Length; i++)
        {
            var targetPos = OtherTargets[i];
            var posDifference = OtherTargets[index % OtherTargets.Length] - targetPos;
            if (posDifference.magnitude > DestinationThreshold)
                continue;
            averageSpread += posDifference.normalized / 2;
            averageVelocity += Velocities[i] / 2;
            averagePosition += targetPos / 2;
        }

        Accelerations[index] += (averageSpread / Count) * Weights.x +
            (averageVelocity / Count) * Weights.y +
            (averagePosition - Positions[index]) * Weights.z;
    }
}