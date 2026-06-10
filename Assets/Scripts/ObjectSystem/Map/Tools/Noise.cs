using UnityEngine;
using TheRavine.Generator;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using System;

public class Noise : IDisposable
{
    private FastNoiseLite heightNoise;
    private FastNoiseLite riverNoise;
    private FastNoiseLite temperatureNoise;
    private FastNoiseLite moistureNoise;
    private NativeArray<float> tempHalf, moistHalf;

    private int chunkSize, halfSize, halfPixels;

    public Noise(
        NoiseLayerSettings heightSettings,
        NoiseLayerSettings riverSettings,
        NoiseLayerSettings temperatureSettings,
        NoiseLayerSettings moistureSettings,
        int seed,
        int _chunkSize)
    {
        chunkSize = _chunkSize;

        halfSize    = chunkSize >> 1;
        halfPixels  = halfSize * halfSize;

        tempHalf  = new NativeArray<float>(halfPixels, Allocator.Persistent);
        moistHalf = new NativeArray<float>(halfPixels, Allocator.Persistent);

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

        HeightMapJob hJob = new HeightMapJob
        {
            ChunkSize = chunkSize,
            WorldX = worldX,
            WorldY = worldY,
            Noise = heightNoise,
            Output = heightMap
        };

        RiverMapJob rJob = new RiverMapJob
        {
            ChunkSize = chunkSize,
            WorldX = worldX,
            WorldY = worldY,
            Noise = riverNoise,
            Output = riverMap
        };

        ClimateHalfJob cJob = new ClimateHalfJob
        {
            HalfSize = halfSize,
            WorldX = worldX,
            WorldY = worldY,
            TempNoise = temperatureNoise,
            MoistNoise = moistureNoise,
            TempHalf = tempHalf,
            MoistHalf = moistHalf
        };


        JobHandle hHandle = hJob.ScheduleParallel(chunkSize, 8, default);

        JobHandle rHandle = rJob.ScheduleParallel(chunkSize, 8, default);

        JobHandle cHandle = cJob.ScheduleParallel(halfSize, 8, default);

        JobHandle tHandle = new Upscale2xJob
        {
            SourceSize = halfSize,
            DestSize = chunkSize,
            Source = tempHalf,
            Dest = temperatureMap
        }.Schedule(cHandle);

        JobHandle mHandle = new Upscale2xJob
        {
            SourceSize = halfSize,
            DestSize = chunkSize,
            Source = moistHalf,
            Dest = moistureMap
        }.Schedule(cHandle);

        JobHandle climateDone = JobHandle.CombineDependencies(tHandle, mHandle);

        JobHandle final = JobHandle.CombineDependencies(
            hHandle,
            rHandle,
            climateDone);

        final.Complete();
    }

    public void Dispose()
    {
        tempHalf.Dispose();
        moistHalf.Dispose();
    }

    [BurstCompile]
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

    [BurstCompile]
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

    [BurstCompile]
    public struct ClimateHalfJob : IJobFor
    {
        [ReadOnly] public int HalfSize;
        [ReadOnly] public int WorldX;
        [ReadOnly] public int WorldY;

        [ReadOnly] public FastNoiseLite TempNoise;
        [ReadOnly] public FastNoiseLite MoistNoise;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> TempHalf;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> MoistHalf;

        public void Execute(int y)
        {
            int row = y * HalfSize;
            int wy = WorldY + y * 2;

            for (int x = 0; x < HalfSize; x++)
            {
                int wx = WorldX + x * 2;
                int index = row + x;

                TempHalf[index] =
                    TempNoise.GetNoise(wx, wy) * 0.5f + 0.5f;

                MoistHalf[index] =
                    MoistNoise.GetNoise(wx, wy) * 0.5f + 0.5f;
            }
        }
    }

    [BurstCompile]
    public struct Upscale2xJob : IJob
    {
        [ReadOnly]
        public int SourceSize;

        [ReadOnly]
        public int DestSize;

        [ReadOnly]
        public NativeArray<float> Source;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> Dest;

        public void Execute()
        {
            const float scale = 0.5f;

            for (int dy = 0; dy < DestSize; dy++)
            {
                float gy = dy * scale;

                int y0 = (int)gy;
                int y1 = math.min(y0 + 1, SourceSize - 1);

                float ty = gy - y0;

                int row0 = y0 * SourceSize;
                int row1 = y1 * SourceSize;
                int dstRow = dy * DestSize;

                for (int dx = 0; dx < DestSize; dx++)
                {
                    float gx = dx * scale;

                    int x0 = (int)gx;
                    int x1 = math.min(x0 + 1, SourceSize - 1);

                    float tx = gx - x0;

                    float c00 = Source[row0 + x0];
                    float c10 = Source[row0 + x1];
                    float c01 = Source[row1 + x0];
                    float c11 = Source[row1 + x1];

                    float a = math.lerp(c00, c10, tx);
                    float b = math.lerp(c01, c11, tx);

                    Dest[dstRow + dx] = math.lerp(a, b, ty);
                }
            }
        }
    }
}