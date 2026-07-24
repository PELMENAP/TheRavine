using UnityEngine;
using TheRavine.Generator;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class Noise
{
    private FastNoiseLite heightNoise;
    private FastNoiseLite heightNoiseDouble;
    private FastNoiseLite riverNoise;
    private FastNoiseLite temperatureNoise;
    private FastNoiseLite moistureNoise;
    private const int chunkSize = MapGenerator.mapChunkSize;
    private const int halfSize = MapGenerator.mapChunkSize >> 1;

    public Noise(
        NoiseLayerSettings heightSettings,
        NoiseLayerSettings riverSettings,
        NoiseLayerSettings temperatureSettings,
        NoiseLayerSettings moistureSettings,
        int seed)
    {
        heightNoise = BuildNoise(heightSettings, seed);
        heightNoiseDouble = BuildNoise(heightSettings, seed * 2);

        riverNoise  = BuildNoise(riverSettings,  seed * 3);
        temperatureNoise = BuildNoise(temperatureSettings, seed * 4);
        moistureNoise    = BuildNoise(moistureSettings,    seed * 5);
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
        NativeArray<float> temperatureMap,
        NativeArray<float> moistureMap,
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
            HeightOutput = heightMap,
            RiverOutput = riverMap
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

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct HeightRiverMapJob : IJobFor
    {
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;
        [ReadOnly] public FastNoiseLite HeightNoise;
        [ReadOnly] public FastNoiseLite HeightNoise2;
        [ReadOnly] public FastNoiseLite RiverNoise;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> HeightOutput;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> RiverOutput;

        public void Execute(int y)
        {
            int row = y * chunkSize;
            int wy = WorldY + y;

            for (int x = 0; x < chunkSize; x++)
            {
                HeightOutput[row + x] =
                    HeightNoise.GetNoise(WorldX + x, wy) * 0.5f + 0.5f + HeightNoise2.GetNoise(WorldX + x, wy) - 2f;

                RiverOutput[row + x] =
                    RiverNoise.GetNoise(WorldX + x, wy) * 0.5f + 0.5f;
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

