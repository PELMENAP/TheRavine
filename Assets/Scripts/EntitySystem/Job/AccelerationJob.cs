using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    [BurstCompile]
    public struct AccelerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float2> OtherTargets;
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [WriteOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DestinationThreshold, AvoidanceThreshold, RandomnessRadius;
        [ReadOnly] public int RandomSeed;
        [ReadOnly] public float3 Weights;
        public void Execute(int index)
        {
            if (!IsMoving[index])
                return;

            Accelerations[index] = float2.zero;
            var random = new Unity.Mathematics.Random((uint)(RandomSeed + index));
            float2 averageSpread = float2.zero, averageVelocity = float2.zero, averagePosition = float2.zero;

            for (ushort i = 0; i < Positions.Length; i++)
            {
                if (i == index)
                    continue;
                float2 posDifference = Positions[index] - Positions[i];
                float distance = math.length(posDifference);
                if (distance < DestinationThreshold && distance > 0)
                {
                    averageSpread += math.normalize(posDifference) * (DestinationThreshold - distance);
                    averageVelocity += Velocities[i];
                    averagePosition += Positions[i];
                }
            }
            if (OtherTargets.Length > 0)
            {
                float2 targetPos = OtherTargets[index % OtherTargets.Length];
                float2 posDifference = targetPos - Positions[index];
                if (math.length(posDifference) > DestinationThreshold)
                {
                    averageSpread += math.normalize(posDifference) * (DestinationThreshold - math.length(posDifference));
                    averagePosition += targetPos;
                }
            }

            float2 finalAverageSpread = averageSpread / Positions.Length * Weights.x;
            float2 finalAverageVelocity = averageVelocity / Positions.Length * Weights.y;
            float2 finalAveragePosition = ((averagePosition / Positions.Length) - Positions[index]) * Weights.z;
            Accelerations[index] = finalAverageSpread + finalAverageVelocity + finalAveragePosition;
        }
    }
}