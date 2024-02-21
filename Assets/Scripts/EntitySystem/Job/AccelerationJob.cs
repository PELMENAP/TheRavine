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
        private int Count => Positions.Length;
        public void Execute(int index)
        {
            if (!IsMoving[index])
                return;

            Accelerations[index] = new float2(0, 0);
            var random = new Unity.Mathematics.Random((uint)(RandomSeed + index));
            float2 averageSpread = new float2(0, 0), averageVelocity = new float2(0, 0), averagePosition = new float2(0, 0);

            for (ushort i = 0; i < Count; i++)
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
                float2 targetPos = OtherTargets[index % OtherTargets.Length] + random.NextFloat2Direction() * RandomnessRadius;
                float2 posDifference = targetPos - Positions[index];
                if (math.length(posDifference) > DestinationThreshold)
                {
                    averageSpread += math.normalize(posDifference) * (DestinationThreshold - math.length(posDifference));
                    averagePosition += targetPos;
                }
            }

            float2 finalAverageSpread = averageSpread / Count * Weights.x;
            float2 finalAverageVelocity = averageVelocity / Count * Weights.y;
            float2 finalAveragePosition = ((averagePosition / Count) - Positions[index]) * Weights.z;
            Accelerations[index] = finalAverageSpread + finalAverageVelocity + finalAveragePosition;
        }
    }
}