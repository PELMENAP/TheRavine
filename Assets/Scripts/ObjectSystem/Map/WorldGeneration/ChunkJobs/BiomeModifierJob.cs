using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct BiomeModifierJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heightIn;
        [ReadOnly] public NativeArray<float> riverMap;
        [ReadOnly] public NativeArray<float> temperatureMap;
        [ReadOnly] public NativeArray<float> moistureMap;

        [ReadOnly] public NativeArray<float> biomeCentersT;
        [ReadOnly] public NativeArray<float> biomeCentersM;

        [ReadOnly] public NativeArray<float> biomeHeightScale;
        [ReadOnly] public NativeArray<float> biomeHeightOffset;

        [ReadOnly] public NativeArray<byte> biomeHasRiver;

        [ReadOnly] public NativeArray<float> riverBedHeight;
        [ReadOnly] public NativeArray<float> riverCenter;
        [ReadOnly] public NativeArray<float> riverRadius;
        [ReadOnly] public NativeArray<float> riverRadiusRcp;
        [WriteOnly] public NativeArray<float> heightOut;
        public float blendRadiusRcp2;
        public float altitudeCooling;

        public void Execute(int idx)
        {
            float baseH = heightIn[idx];

            float temp =
                math.saturate(
                    temperatureMap[idx] -
                    baseH * altitudeCooling);

            float moisture = moistureMap[idx];
            float riverValue = riverMap[idx];

            float totalWeight = 0f;
            float scaleSum = 0f;
            float offsetSum = 0f;

            float riverWeightSum = 0f;
            float riverBedSum = 0f;

            int biomeCount = biomeCentersT.Length;

            for (int b = 0; b < biomeCount; b++)
            {
                float dt = temp - biomeCentersT[b];
                float dm = moisture - biomeCentersM[b];

                float dist2 =
                    (dt * dt + dm * dm) * blendRadiusRcp2;

                float weight = math.max(0f, 1f - dist2);
                weight = weight * weight * (3f - 2f * weight);

                if (weight <= 0f)
                    continue;

                totalWeight += weight;
                scaleSum += weight * biomeHeightScale[b];
                offsetSum += weight * biomeHeightOffset[b];

                if (biomeHasRiver[b] == 0)
                    continue;

                float dist = math.abs(riverValue - riverCenter[b]);

                if (dist >= riverRadius[b])
                    continue;

                float riverT = 1f - dist * riverRadiusRcp[b];
                riverT = riverT * riverT * (3f - 2f * riverT);

                riverWeightSum += weight * riverT;
                riverBedSum += weight * riverBedHeight[b];
            }

            if (totalWeight < 0.0001f)
            {
                heightOut[idx] = baseH;
                return;
            }

            float rcpWeight =
                math.rcp(totalWeight);

            float resultHeight =
                math.saturate(
                    baseH *
                    (scaleSum * rcpWeight)
                    +
                    offsetSum * rcpWeight);

            float riverBlend =
                riverWeightSum * rcpWeight;

            if (riverBlend > 0f)
            {
                resultHeight =
                    math.lerp(
                        resultHeight,
                        riverBedSum * rcpWeight,
                        riverBlend);
            }

            heightOut[idx] = resultHeight;
        }
    }
}