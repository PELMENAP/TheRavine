using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public struct LandformParams
    {
        public float continentalness;
        public float ruggedness;
        public float heightScale;
        public float heightOffset;
        public float ridgeWeight;
        public float warpAmplitude;
        public float terraceWeight;
        public float terraceStep;
        public float riverWidth;
        public float riverCarve;
        public float riverWarp;
        public float erosion;

        public static LandformParams Identity => new()
        {
            heightScale = 1f,
            riverWidth = 1f,
            riverCarve = 1f,
            erosion = 1f
        };

        public static LandformParams From(in LandformSettings s) => new()
        {
            continentalness = s.continentalness,
            ruggedness = s.ruggedness,
            heightScale = s.heightScale,
            heightOffset = s.heightOffset,
            ridgeWeight = s.ridgeWeight,
            warpAmplitude = s.warpAmplitude,
            terraceWeight = s.terraceWeight,
            terraceStep = s.terraceStep,
            riverWidth = s.riverWidth,
            riverCarve = s.riverCarve,
            riverWarp = s.riverWarp,
            erosion = s.erosion
        };
    }

    public static class LandformBlend
    {
        private const float MinTotalWeight = 1e-6f;

        public static void Control(
            ref FastNoiseLite continentalNoise,
            ref FastNoiseLite ruggednessNoise,
            float x,
            float y,
            float contrast,
            out float c,
            out float r)
        {
            float cv = continentalNoise.GetNoise(x, y) * 0.5f + 0.5f;
            float rv = ruggednessNoise.GetNoise(x, y) * 0.5f + 0.5f;
            c = math.saturate((cv - 0.5f) * contrast + 0.5f);
            r = math.saturate((rv - 0.5f) * contrast + 0.5f);
        }

        public static void Evaluate(
            float c,
            float r,
            NativeArray<LandformParams> presets,
            float blendRadiusRcp2,
            float baseWeight,
            out LandformParams result)
        {
            float total = baseWeight;

            float scale = 0f;
            float offset = 0f;
            float ridge = 0f;
            float warp = 0f;
            float terrace = 0f;
            float terraceStepSum = 0f;
            float width = 0f;
            float carve = 0f;
            float riverWarp = 0f;
            float erosion = 0f;

            for (int i = 0; i < presets.Length; i++)
            {
                LandformParams p = presets[i];

                float dc = c - p.continentalness;
                float dr = r - p.ruggedness;
                float w = math.saturate(1f - (dc * dc + dr * dr) * blendRadiusRcp2);
                w = w * w * (3f - 2f * w);

                total += w;

                scale += w * (p.heightScale - 1f);
                offset += w * p.heightOffset;
                ridge += w * p.ridgeWeight;
                warp += w * p.warpAmplitude;
                terrace += w * p.terraceWeight;
                terraceStepSum += w * p.terraceWeight * p.terraceStep;
                width += w * (p.riverWidth - 1f);
                carve += w * (p.riverCarve - 1f);
                riverWarp += w * p.riverWarp;
                erosion += w * (p.erosion - 1f);
            }

            float rcpTotal = 1f / math.max(total, MinTotalWeight);

            result = LandformParams.Identity;
            result.continentalness = c;
            result.ruggedness = r;
            result.heightScale += scale * rcpTotal;
            result.heightOffset = offset * rcpTotal;
            result.ridgeWeight = ridge * rcpTotal;
            result.warpAmplitude = warp * rcpTotal;
            result.terraceWeight = terrace * rcpTotal;
            result.terraceStep = terrace > 0f ? terraceStepSum / terrace : 0f;
            result.riverWidth += width * rcpTotal;
            result.riverCarve += carve * rcpTotal;
            result.riverWarp = riverWarp * rcpTotal;
            result.erosion += erosion * rcpTotal;
        }
    }
}
