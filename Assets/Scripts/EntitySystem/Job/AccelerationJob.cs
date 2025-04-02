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
            new int2(-1, 0),  new int2(0, 0),  new int2(1, 0),
            new int2(-1, 1),  new int2(0, 1),  new int2(1, 1)
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

            float2 forwardDir = math.normalizesafe(vel);
            int2 dirCellOffset = new int2((int)math.sign(forwardDir.x), (int)math.sign(forwardDir.y));

            float AvoidanceThresholdSq = AvoidanceThreshold * AvoidanceThreshold, AlongThresholdSq = AlongThreshold * AlongThreshold;

            for (int i = 0; i < 5; i++)
            {
                int2 neighborCell = cellPos + s_CellOffsets[i] + dirCellOffset;
                
                if (SpatialGrid.TryGetFirstValue(neighborCell, out int neighborIndex, out var iterator))
                {
                    do
                    {
                        if (neighborIndex == index || FlockIds[neighborIndex] != flockId || !IsMoving[neighborIndex])
                            continue;
                        
                        float2 neighborPos = PositionsAndVelocities[neighborIndex].xy;
                        float2 neighborVel = PositionsAndVelocities[neighborIndex].zw;
                        
                        float2 posDifference = pos - neighborPos;
                        float distanceSq = math.lengthsq(posDifference);
                        
                        if (distanceSq < AvoidanceThresholdSq && distanceSq > 0)
                        {
                            float distFactor = AvoidanceThreshold - distanceSq * math.rsqrt(distanceSq);
                            separation += posDifference * math.rsqrt(distanceSq) * distFactor;
                        }
                        
                        if (distanceSq < AlongThresholdSq)
                        {
                            alignment += neighborVel;
                            cohesion += neighborPos;
                            neighborCount++;
                        }
                    } while (SpatialGrid.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }
            
            if (neighborCount > 0)
            {
                float invNeighborCount = 1.0f / neighborCount;
                cohesion = (cohesion * invNeighborCount - pos) * Weights.z;
                alignment = alignment * invNeighborCount * Weights.y;
            }
            
            float2 targetDirection = OtherTargets[flockId] - pos;
            float targetDistanceSq = math.lengthsq(targetDirection);
            
            if (targetDistanceSq > DestinationThreshold * DestinationThreshold)
            {
                float invTargetDistance = math.rsqrt(targetDistanceSq);
                float targetDistance = math.sqrt(targetDistanceSq);
                separation += targetDirection * invTargetDistance * (targetDistance - DestinationThreshold);
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