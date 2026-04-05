using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct AccelerationJob : IJobParallelFor
    {
        [ReadOnly][NativeDisableParallelForRestriction] public NativeArray<float4> PositionsAndVelocities;
        [ReadOnly] public NativeArray<float2> OtherTargets;
        [ReadOnly] public NativeArray<int> FlockIds;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [WriteOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DestinationThreshold, AvoidanceThreshold, AlongThreshold;
        [ReadOnly] public float3 Weights;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialGrid;
        [ReadOnly] public float InvCellSize;

        readonly static int2[] s_CellOffsets = new int2[]
        {
            new int2(-1, -1), new int2(0, -1), new int2(1, -1),
            new int2(-1,  0), new int2(0,  0), new int2(1,  0),
            new int2(-1,  1), new int2(0,  1), new int2(1,  1)
        };

        public void Execute(int index)
        {
            if (!IsMoving[index]) return;

            Accelerations[index] = float2.zero;
            float2 separation = float2.zero, alignment = float2.zero, cohesion = float2.zero;
            int flockId = FlockIds[index];
            int neighborCount = 0;

            float2 pos = PositionsAndVelocities[index].xy;
            float2 vel = PositionsAndVelocities[index].zw;

            int2 cellPos = new int2(
                (int)math.floor(pos.x * InvCellSize),
                (int)math.floor(pos.y * InvCellSize)
            );

            float avoidanceSq = AvoidanceThreshold * AvoidanceThreshold;
            float alongSq = AlongThreshold * AlongThreshold;

            for (int i = 0; i < s_CellOffsets.Length; i++)
            {
                int2 neighborCell = cellPos + s_CellOffsets[i];

                if (!SpatialGrid.TryGetFirstValue(neighborCell, out int neighborIndex, out var iterator))
                    continue;

                do
                {
                    if (neighborIndex == index || FlockIds[neighborIndex] != flockId || !IsMoving[neighborIndex])
                        continue;

                    float2 neighborPos = PositionsAndVelocities[neighborIndex].xy;
                    float2 neighborVel = PositionsAndVelocities[neighborIndex].zw;

                    float2 diff = pos - neighborPos;
                    float distSq = math.lengthsq(diff);

                    if (distSq < avoidanceSq && distSq > 0f)
                    {
                        float invDist = math.rsqrt(distSq);
                        float distFactor = AvoidanceThreshold - distSq * invDist;
                        separation += diff * invDist * distFactor;
                    }

                    if (distSq < alongSq)
                    {
                        alignment += neighborVel;
                        cohesion += neighborPos;
                        neighborCount++;
                    }
                }
                while (SpatialGrid.TryGetNextValue(out neighborIndex, ref iterator));
            }

            if (neighborCount > 0)
            {
                float invN = 1f / neighborCount;
                cohesion = (cohesion * invN - pos) * Weights.z;
                alignment = alignment * invN * Weights.y;
            }

            float2 toTarget = OtherTargets[flockId] - pos;
            float targetDistSq = math.lengthsq(toTarget);

            if (targetDistSq > DestinationThreshold * DestinationThreshold)
            {
                float invTargetDist = math.rsqrt(targetDistSq);
                float targetDist = targetDistSq * invTargetDist;
                separation += toTarget * invTargetDist * (targetDist - DestinationThreshold);
            }

            Accelerations[index] = separation * Weights.x + alignment + cohesion;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct InitSpatialGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> PositionsAndVelocities;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [WriteOnly] public NativeParallelMultiHashMap<int2, int>.ParallelWriter SpatialGrid;
        [ReadOnly] public float InvCellSize;

        public void Execute(int i)
        {
            if (!IsMoving[i]) return;

            float2 pos = PositionsAndVelocities[i].xy;
            int2 cellPos = new int2(
                (int)math.floor(pos.x * InvCellSize),
                (int)math.floor(pos.y * InvCellSize)
            );
            SpatialGrid.Add(cellPos, i);
        }
    }
}