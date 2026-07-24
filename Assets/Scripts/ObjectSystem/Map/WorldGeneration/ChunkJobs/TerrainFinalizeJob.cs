using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct TerrainFinalizeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heightValues;
        [ReadOnly] public NativeArray<float> temperatureValues;
        [ReadOnly] public NativeArray<float> moistureValues;
        [ReadOnly] public NativeArray<float> regionThresholds;
        [ReadOnly] public NativeArray<float> biomeCentersT;
        [ReadOnly] public NativeArray<float> biomeCentersM;
        [ReadOnly] public int mountainRegionIndex;
        private const float cellWorldDist = 1f / (MapGenerator.scale * 2f);

        [WriteOnly] public NativeArray<int> regionMap;
        [WriteOnly] public NativeArray<int> biomeMap;
        [WriteOnly] public NativeArray<byte> moveCost;

        private const float MaxGradient = 2.7475f;
        private const float InvMaxGradient = 1f / MaxGradient;

        public void Execute(int index)
        {
            float h = heightValues[index];
            int regionIndex = 0;
            for (int i = 0; i < regionThresholds.Length; i++)
            {
                if (h >= regionThresholds[i]) regionIndex = i;
                else break;
            }
            regionMap[index] = regionIndex;

            if (regionIndex == mountainRegionIndex)
            {
                biomeMap[index] = 0;
            }
            else
            {
                float temp = temperatureValues[index];
                float moist = moistureValues[index];
                int best = 0;
                float bestD2 = float.MaxValue;
                for (int b = 0; b < biomeCentersT.Length; b++)
                {
                    float dt = temp - biomeCentersT[b];
                    float dm = moist - biomeCentersM[b];
                    float d2 = dt * dt + dm * dm;
                    if (d2 < bestD2) { bestD2 = d2; best = b; }
                }
                biomeMap[index] = best;
            }

            int x = index % MapGenerator.mapChunkSize;
            int z = index / MapGenerator.mapChunkSize;

            int x0 = math.max(0, x - 1);
            int x1 = math.min(MapGenerator.mapChunkSize - 1, x + 1);
            int z0 = math.max(0, z - 1);
            int z1 = math.min(MapGenerator.mapChunkSize - 1, z + 1);

            float scale = MapGenerator.maxTerrainHeight * cellWorldDist;
            float dx = (heightValues[z * MapGenerator.mapChunkSize + x1] - heightValues[z * MapGenerator.mapChunkSize + x0]) * scale;
            float dz = (heightValues[z1 * MapGenerator.mapChunkSize + x]  - heightValues[z0 * MapGenerator.mapChunkSize + x])  * scale;

            float gradient = math.sqrt(dx * dx + dz * dz);
            if (gradient >= MaxGradient)
            {
                moveCost[index] = 0;
                return;
            }
            moveCost[index] = (byte)math.clamp((1f - gradient * InvMaxGradient) * 255f, 0f, 255f);
        }
    }
}

