using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct SlopeMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heightMap;
        [ReadOnly] public int mapSize;
        [WriteOnly] public NativeArray<byte> moveCost;
        [WriteOnly] public NativeArray<float2> slopeDirection;

        public void Execute(int index)
        {
            int x = index % mapSize;
            int z = index / mapSize;

            int x0 = math.max(0, x - 1);
            int x1 = math.min(mapSize - 1, x + 1);

            int z0 = math.max(0, z - 1);
            int z1 = math.min(mapSize - 1, z + 1);

            float hL = heightMap[z * mapSize + x0];
            float hR = heightMap[z * mapSize + x1];

            float hD = heightMap[z0 * mapSize + x];
            float hU = heightMap[z1 * mapSize + x];

            float dx = hR - hL;
            float dz = hU - hD;

            float2 uphill = new(dx, dz);

            float lenSq = math.lengthsq(uphill);

            if (lenSq < 0.000001f)
            {
                slopeDirection[index] = new float2(0, 0);
                moveCost[index] = byte.MaxValue;
                return;
            }

            float invLen = math.rsqrt(lenSq);

            uphill *= invLen;

            slopeDirection[index] = uphill;

            float steepness = math.saturate(
                math.sqrt(lenSq) * 4f);

            moveCost[index] =
                (byte)math.round(
                    (1f - steepness) * 255f);
        }
    }
}