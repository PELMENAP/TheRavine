using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct RegionAssignJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heightValues;
        [ReadOnly] public NativeArray<float> temperatureValues;
        [ReadOnly] public NativeArray<float> moistureValues;
        [ReadOnly] public NativeArray<float> regionThresholds;
        [ReadOnly] public NativeArray<float> biomeCentersT;
        [ReadOnly] public NativeArray<float> biomeCentersM;
        [ReadOnly] public int mountainRegionIndex;
        [WriteOnly] public NativeArray<int> heightMap;
        [WriteOnly] public NativeArray<int> biomeMap;
        public void Execute(int index)
        {
            float h = heightValues[index];

            int regionIndex = 0;

            for (int i = 0; i < regionThresholds.Length; i++)
            {
                if (h >= regionThresholds[i])
                    regionIndex = i;
                else
                    break;
            }

            heightMap[index] = regionIndex;

            if (regionIndex == mountainRegionIndex)
            {
                biomeMap[index] = 0;
                return;
            }

            float temp = temperatureValues[index];
            float moist = moistureValues[index];

            int best = 0;
            float bestD2 = float.MaxValue;

            for (int b = 0; b < biomeCentersT.Length; b++)
            {
                float dt = temp - biomeCentersT[b];
                float dm = moist - biomeCentersM[b];

                float d2 = dt * dt + dm * dm;

                if (d2 >= bestD2)
                    continue;

                bestD2 = d2;
                best = b;
            }

            biomeMap[index] = best;
        }
    }
}