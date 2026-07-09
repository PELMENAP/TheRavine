using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct SlopeMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heightMap;
        [ReadOnly] public int    mapSize;
        [ReadOnly] public float  heightScale;
        [ReadOnly] public float  cellWorldDist;

        [WriteOnly] public NativeArray<byte>   moveCost;

        // tan(70°)
        private const float MaxGradient    = 2.7475f;
        private const float InvMaxGradient = 1f / MaxGradient;

        public void Execute(int index)
        {
            int x = index % mapSize;
            int z = index / mapSize;

            int x0 = math.max(0, x - 1);
            int x1 = math.min(mapSize - 1, x + 1);
            int z0 = math.max(0, z - 1);
            int z1 = math.min(mapSize - 1, z + 1);

            float scale = heightScale / cellWorldDist;

            float dx = (heightMap[z  * mapSize + x1] - heightMap[z  * mapSize + x0]) * scale;
            float dz = (heightMap[z1 * mapSize + x]  - heightMap[z0 * mapSize + x])  * scale;

            float gradient = math.sqrt(dx * dx + dz * dz);

            if (gradient >= MaxGradient)
            {
                moveCost[index] = 0;
                return;
            }

            float steepness = gradient * InvMaxGradient;
            moveCost[index] = (byte)math.round((1f - steepness) * 255f);
        }
    }
}