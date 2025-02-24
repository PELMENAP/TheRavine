using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    [BurstCompile]
    public struct AccelerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions, OtherTargets, Velocities;
        [ReadOnly] public NativeArray<int> FlockIds;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [WriteOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DestinationThreshold, AvoidanceThreshold, AlongThreshold;
        [ReadOnly] public float3 Weights;

        public void Execute(int index)
        {
            if (!IsMoving[index]) return;
            Accelerations[index] = float2.zero;

            float2 separation = float2.zero, alignment = float2.zero, cohesion = float2.zero;
            int flockId = FlockIds[index];
            int neighborCount = 0;

            for (int i = 0; i < Positions.Length; i++)
            {
                if (i == index || FlockIds[i] != flockId || !IsMoving[i]) continue;

                float2 posDifference = Positions[index] - Positions[i];
                float distanceSq = math.lengthsq(posDifference);

                if (distanceSq < AvoidanceThreshold * AvoidanceThreshold && distanceSq > 0)
                {   
                    separation += math.select(
                        posDifference * math.rsqrt(math.lengthsq(posDifference)) * (AvoidanceThreshold - math.sqrt(distanceSq)),
                        float2.zero,
                        distanceSq < 0.0001f
                    );
                }
                if (distanceSq < AlongThreshold * AlongThreshold)
                {
                    alignment += Velocities[i];
                    cohesion += Positions[i];
                    neighborCount++;
                }
            }

            if (neighborCount > 0)
            {
                cohesion = (cohesion / neighborCount - Positions[index]) * Weights.z;
                alignment = (alignment / neighborCount) * Weights.y;
            }

            float2 targetDirection = OtherTargets[flockId] - Positions[index];
            float distanceToTarget = math.length(targetDirection);
            if (distanceToTarget > DestinationThreshold)
            {
                separation += targetDirection * math.rsqrt(math.lengthsq(targetDirection)) * (distanceToTarget - DestinationThreshold);
            }

            Accelerations[index] = separation * Weights.x + alignment + cohesion;
        }
    }

}