using UnityEngine;
using TheRavine.Generator;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

public class Noise
{
    private FastNoiseLite heightNoise;
    private FastNoiseLite heightNoiseDouble;
    private FastNoiseLite riverNoise;
    private FastNoiseLite temperatureNoise;
    private FastNoiseLite moistureNoise;
    private FastNoiseLite continentalnessNoise;
    private FastNoiseLite ruggednessNoise;
    private FastNoiseLite ridgeNoise;
    private FastNoiseLite warpNoise;
    private readonly float landformContrast;
    private readonly float landformBlendRadiusRcp2;
    private readonly float landformBaseWeight;
    private const int chunkSize = MapGenerator.mapChunkSize;
    private const int halfSize = MapGenerator.mapChunkSize >> 1;

    public Noise(ChunkGenerationSettings settings, int seed)
    {
        heightNoise = BuildNoise(settings.heightNoiseSettings, seed);
        heightNoiseDouble = BuildNoise(settings.heightNoiseSettings, seed * 2);

        riverNoise  = BuildNoise(settings.riverNoiseSettings,  seed * 3);
        temperatureNoise = BuildNoise(settings.temperatureSettings, seed * 4);
        moistureNoise    = BuildNoise(settings.moistureSettings,    seed * 5);

        continentalnessNoise = BuildNoise(settings.continentalnessSettings, seed * 6);
        ruggednessNoise      = BuildNoise(settings.ruggednessSettings,      seed * 7);
        ridgeNoise           = BuildNoise(settings.ridgeNoiseSettings,      seed * 8);
        warpNoise            = BuildNoise(settings.landformWarpSettings,    seed * 9);
        warpNoise.SetDomainWarpAmp(1f);

        landformContrast = settings.landformContrast;
        landformBlendRadiusRcp2 = 1f / (settings.landformBlendRadius * settings.landformBlendRadius);
        landformBaseWeight = settings.landformBaseWeight;
    }

    private FastNoiseLite BuildNoise(in NoiseLayerSettings s, int seed)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(s.noiseType);
        noise.SetFractalType(s.fractalType);
        noise.SetFrequency(s.frequency);
        noise.SetFractalOctaves(s.octaves);
        noise.SetFractalLacunarity(s.lacunarity);
        noise.SetFractalGain(s.gain);
        noise.SetFractalWeightedStrength(s.weightedStrength);
        return noise;
    }

    public JobHandle GenerateAllMaps(
        NativeArray<float> heightMap,
        NativeArray<float> riverMap,
        NativeArray<float2> riverShapeMap,
        NativeArray<float> erosionScaleMap,
        NativeArray<float> temperatureMap,
        NativeArray<float> moistureMap,
        NativeArray<LandformParams> landforms,
        Vector2Int chunkOffset)
    {
        int worldX = chunkOffset.x * chunkSize;
        int worldY = chunkOffset.y * chunkSize;

        HeightRiverMapJob hrJob = new()
        {
            WorldX = worldX,
            WorldY = worldY,
            HeightNoise = heightNoise,
            HeightNoise2 = heightNoiseDouble,
            RiverNoise = riverNoise,
            ContinentalnessNoise = continentalnessNoise,
            RuggednessNoise = ruggednessNoise,
            RidgeNoise = ridgeNoise,
            WarpNoise = warpNoise,
            Landforms = landforms,
            Contrast = landformContrast,
            BlendRadiusRcp2 = landformBlendRadiusRcp2,
            BaseWeight = landformBaseWeight,
            HeightOutput = heightMap,
            RiverOutput = riverMap,
            RiverShapeOutput = riverShapeMap,
            ErosionOutput = erosionScaleMap
        };

        ClimateDirect2xJob cJob = new()
        {
            WorldX = worldX,
            WorldY = worldY,
            TempNoise = temperatureNoise,
            MoistNoise = moistureNoise,
            TemperatureMap = temperatureMap,
            MoistureMap = moistureMap
        };

        JobHandle hrHandle = hrJob.ScheduleParallel(chunkSize, 8, default);

        JobHandle cHandle = cJob.ScheduleParallel(halfSize, 8, default);

        return JobHandle.CombineDependencies(hrHandle, cHandle);

    }

    public JobHandle GenerateErosionScale(
        NativeArray<float> erosionScaleMap,
        NativeArray<LandformParams> landforms,
        Vector2Int chunkOffset)
    {
        LandformErosionJob job = new()
        {
            WorldX = chunkOffset.x * chunkSize,
            WorldY = chunkOffset.y * chunkSize,
            ContinentalnessNoise = continentalnessNoise,
            RuggednessNoise = ruggednessNoise,
            Landforms = landforms,
            Contrast = landformContrast,
            BlendRadiusRcp2 = landformBlendRadiusRcp2,
            BaseWeight = landformBaseWeight,
            ErosionOutput = erosionScaleMap
        };

        return job.ScheduleParallel(chunkSize, 8, default);
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct HeightRiverMapJob : IJobFor
    {
        private const float RidgeScale = 1.5f;
        private const float RidgeOffset = 0.5f;

        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;
        [ReadOnly] public FastNoiseLite HeightNoise;
        [ReadOnly] public FastNoiseLite HeightNoise2;
        [ReadOnly] public FastNoiseLite RiverNoise;
        [ReadOnly] public FastNoiseLite ContinentalnessNoise;
        [ReadOnly] public FastNoiseLite RuggednessNoise;
        [ReadOnly] public FastNoiseLite RidgeNoise;
        [ReadOnly] public FastNoiseLite WarpNoise;
        [ReadOnly] public NativeArray<LandformParams> Landforms;
        [ReadOnly] public float Contrast;
        [ReadOnly] public float BlendRadiusRcp2;
        [ReadOnly] public float BaseWeight;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> HeightOutput;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> RiverOutput;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float2> RiverShapeOutput;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> ErosionOutput;

        public void Execute(int y)
        {
            int row = y * chunkSize;
            float wy = WorldY + y;

            for (int x = 0; x < chunkSize; x++)
            {
                float wx = WorldX + x;

                LandformBlend.Control(ref ContinentalnessNoise, ref RuggednessNoise, wx, wy, Contrast, out float c, out float r);
                LandformBlend.Evaluate(c, r, Landforms, BlendRadiusRcp2, BaseWeight, out LandformParams p);

                float dx = 0f;
                float dy = 0f;
                if (p.warpAmplitude != 0f || p.riverWarp != 0f)
                {
                    float ux = wx;
                    float uy = wy;
                    WarpNoise.DomainWarp(ref ux, ref uy);
                    dx = ux - wx;
                    dy = uy - wy;
                }

                float hx = wx + dx * p.warpAmplitude;
                float hy = wy + dy * p.warpAmplitude;

                float h = HeightNoise.GetNoise(hx, hy) * 0.5f + 0.5f + HeightNoise2.GetNoise(hx, hy);

                if (p.ridgeWeight > 0f)
                {
                    float ridge = RidgeNoise.GetNoise(hx, hy) * RidgeScale + RidgeOffset;
                    h = math.lerp(h, ridge, p.ridgeWeight);
                }

                if (p.terraceWeight > 0f && p.terraceStep > 0f)
                {
                    float t = h / p.terraceStep;
                    float f = math.frac(t);
                    float terraced = (math.floor(t) + f * f * (3f - 2f * f)) * p.terraceStep;
                    h = math.lerp(h, terraced, p.terraceWeight);
                }

                int i = row + x;
                HeightOutput[i] = h * p.heightScale + p.heightOffset;
                RiverOutput[i] = RiverNoise.GetNoise(wx + dx * p.riverWarp, wy + dy * p.riverWarp) * 0.5f + 0.5f;
                RiverShapeOutput[i] = new float2(p.riverWidth, p.riverCarve);
                ErosionOutput[i] = p.erosion;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct LandformErosionJob : IJobFor
    {
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;
        [ReadOnly] public FastNoiseLite ContinentalnessNoise;
        [ReadOnly] public FastNoiseLite RuggednessNoise;
        [ReadOnly] public NativeArray<LandformParams> Landforms;
        [ReadOnly] public float Contrast;
        [ReadOnly] public float BlendRadiusRcp2;
        [ReadOnly] public float BaseWeight;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> ErosionOutput;

        public void Execute(int y)
        {
            int row = y * chunkSize;
            float wy = WorldY + y;

            for (int x = 0; x < chunkSize; x++)
            {
                LandformBlend.Control(ref ContinentalnessNoise, ref RuggednessNoise, WorldX + x, wy, Contrast, out float c, out float r);
                LandformBlend.Evaluate(c, r, Landforms, BlendRadiusRcp2, BaseWeight, out LandformParams p);
                ErosionOutput[row + x] = p.erosion;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct ClimateDirect2xJob : IJobFor
    {
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;

        [ReadOnly] public FastNoiseLite TempNoise;
        [ReadOnly] public FastNoiseLite MoistNoise;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> TemperatureMap;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> MoistureMap;

        public void Execute(int hy)
        {
            int y = hy * 2;
            int wy = WorldY + y;

            int row0 = y * chunkSize;
            int row1 = row0 + chunkSize;

            for (int hx = 0; hx < halfSize; hx++)
            {
                int x = hx * 2;
                int wx = WorldX + x;

                float temp = TempNoise.GetNoise(wx, wy) * 0.5f + 0.5f;
                float moist = MoistNoise.GetNoise(wx, wy) * 0.5f + 0.5f;

                int i00 = row0 + x;
                int i10 = i00 + 1;
                int i01 = row1 + x;
                int i11 = i01 + 1;

                TemperatureMap[i00] = temp;
                TemperatureMap[i10] = temp;
                TemperatureMap[i01] = temp;
                TemperatureMap[i11] = temp;

                MoistureMap[i00] = moist;
                MoistureMap[i10] = moist;
                MoistureMap[i01] = moist;
                MoistureMap[i11] = moist;
            }
        }
    }
}

