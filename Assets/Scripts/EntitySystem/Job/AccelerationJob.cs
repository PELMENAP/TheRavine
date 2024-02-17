using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
    [ReadOnly]
    public NativeArray<bool> IsMoving;

    public NativeArray<Vector3> Accelerations;

    [ReadOnly]
    public float DestinationThreshold, AvoidanceThreshold, RandomnessRadius;
    [ReadOnly]
    public int RandomSeed;
    [ReadOnly]
    public Vector3 Weights;
    private int Count => Positions.Length;
    public void Execute(int index)
    {
        if (!IsMoving[index])
            return;

        Accelerations[index] = Vector3.zero;
        var random = new Unity.Mathematics.Random((uint)(RandomSeed + index));
        Vector3 averageSpread = Vector3.zero,
            averageVelocity = Vector3.zero,
            averagePosition = Vector3.zero;

        int otherTargetsCount = OtherTargets.Length;

        // Обработка взаимодействия с остальными юнитами
        for (int i = 0; i < Count; i++)
        {
            if (i == index)
                continue;

            Vector3 posDifference = Positions[index] - Positions[i];
            posDifference.z = 0;
            if (posDifference.magnitude < DestinationThreshold && posDifference.magnitude > 0)
            {
                averageSpread += posDifference.normalized * (DestinationThreshold - posDifference.magnitude);
                averageVelocity += Velocities[i];
                averagePosition += Positions[i];
            }
        }

        // Пересчет с учетом OtherTargets
        if (otherTargetsCount > 0)
        {
            Vector3 targetPos = OtherTargets[index % otherTargetsCount];
            // Добавляем случайное смещение к цели
            Vector3 randomOffset = random.NextFloat3Direction() * RandomnessRadius;
            targetPos += randomOffset;
            randomOffset.z = 0;
            targetPos.z = 0;

            Vector3 posDifference = targetPos - Positions[index];
            posDifference.z = 0;

            if (posDifference.magnitude > DestinationThreshold)
            {
                averageSpread += posDifference.normalized * (DestinationThreshold - posDifference.magnitude);
                averagePosition += targetPos;
            }
        }

        Vector3 finalAverageSpread = averageSpread / Count;
        Vector3 finalAverageVelocity = averageVelocity / Count;
        Vector3 finalAveragePosition = (averagePosition / Count) - Positions[index];

        Accelerations[index] = (finalAverageSpread * Weights.x) +
                               (finalAverageVelocity * Weights.y) +
                               (finalAveragePosition * Weights.z);
    }
}