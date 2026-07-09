using UnityEngine;
using TheRavine.Generator;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class Noise
{
    private FastNoiseLite heightNoise;
    private FastNoiseLite riverNoise;
    private FastNoiseLite temperatureNoise;
    private FastNoiseLite moistureNoise;
    private int chunkSize, halfSize;


    public Noise(
        NoiseLayerSettings heightSettings,
        NoiseLayerSettings riverSettings,
        NoiseLayerSettings temperatureSettings,
        NoiseLayerSettings moistureSettings,
        int seed,
        int _chunkSize)
    {
        chunkSize = _chunkSize;

        halfSize = chunkSize >> 1;

        heightNoise = BuildNoise(heightSettings, seed);
        riverNoise  = BuildNoise(riverSettings,  seed * 2);
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

    public void GenerateAllMaps(
        NativeArray<float> heightMap,
        NativeArray<float> riverMap,
        NativeArray<float> temperatureMap,
        NativeArray<float> moistureMap,
        Vector2Int chunkOffset)
    {
        int worldX = chunkOffset.x * chunkSize;
        int worldY = chunkOffset.y * chunkSize;

        HeightMapJob hJob = new()
        {
            ChunkSize = chunkSize,
            WorldX = worldX,
            WorldY = worldY,
            Noise = heightNoise,
            Output = heightMap
        };

        RiverMapJob rJob = new()
        {
            ChunkSize = chunkSize,
            WorldX = worldX,
            WorldY = worldY,
            Noise = riverNoise,
            Output = riverMap
        };

        ClimateDirect2xJob cJob = new()
        {
            HalfSize = halfSize,
            ChunkSize = chunkSize,
            WorldX = worldX,
            WorldY = worldY,
            TempNoise = temperatureNoise,
            MoistNoise = moistureNoise,
            TemperatureMap = temperatureMap,
            MoistureMap = moistureMap
        };

        JobHandle hHandle = hJob.ScheduleParallel(chunkSize, 8, default);

        JobHandle rHandle = rJob.ScheduleParallel(chunkSize, 8, default);

        JobHandle cHandle = cJob.ScheduleParallel(halfSize, 8, default);

        JobHandle final = JobHandle.CombineDependencies(
            hHandle,
            rHandle,
            cHandle);

        final.Complete();

    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct HeightMapJob : IJobFor
    {
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;
        [ReadOnly] public FastNoiseLite Noise;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> Output;

        public void Execute(int y)
        {
            int row = y * ChunkSize;
            int wy = WorldY + y;

            for (int x = 0; x < ChunkSize; x++)
            {
                Output[row + x] =
                    Noise.GetNoise(WorldX + x, wy) * 0.5f + 0.5f;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct RiverMapJob : IJobFor
    {
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;
        [ReadOnly] public FastNoiseLite Noise;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> Output;

        public void Execute(int y)
        {
            int row = y * ChunkSize;
            int wy = WorldY + y;

            for (int x = 0; x < ChunkSize; x++)
            {
                Output[row + x] =
                    Noise.GetNoise(WorldX + x, wy) * 0.5f + 0.5f;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct ClimateDirect2xJob : IJobFor
    {
        [ReadOnly] public int HalfSize;
        [ReadOnly] public int ChunkSize;
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

            int row0 = y * ChunkSize;
            int row1 = row0 + ChunkSize;

            for (int hx = 0; hx < HalfSize; hx++)
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