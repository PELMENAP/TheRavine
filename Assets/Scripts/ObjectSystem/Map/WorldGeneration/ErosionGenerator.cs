using System;
using UnityEngine;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public class ErosionGenerator : IDisposable
    {
        private const int mapChunkSize   = MapGenerator.mapChunkSize;
        private ComputeBuffer erosionSourceMapBuffer;
        private ComputeBuffer erosionDeltaBuffer;
        private ComputeBuffer erosionStartPosBuffer;

        private ComputeShader cs;

        private float[]   erosionSourceMap;
        private float[]   erosionDeltaMap;
        private Vector2[] erosionStartPos;

        private int erosionKernel = -1;
        private int cachedDropletCount = -1;
        private int cachedCellCount = -1;

        private int droplets;

        private readonly ChunkGenerationSettings settings;
        public ErosionGenerator(ChunkGenerationSettings _settings)
        {
            settings = _settings;

            cs = settings.erosionShader;

            ErosionSettings e = settings.erosion;
            droplets  = e.dropletCount;

            cs.SetInt("sizeX", mapChunkSize);
            cs.SetInt("sizeY", mapChunkSize);

            cs.SetInt("lifetime", e.SafeLifetime);
            cs.SetInt("dropletCount", droplets);

            cs.SetFloat("startSpeed", e.startSpeed);
            cs.SetFloat("inertia", 1f - e.drag);
            cs.SetFloat("gravity", e.gravity);

            cs.SetFloat("evaporation", e.evaporateSpeed);

            cs.SetFloat("sedimentCapacityFactor", e.sedimentCapacityFactor);

            cs.SetFloat("depositSpeed", e.depositSpeed);
            cs.SetFloat("erodeSpeed", e.erodeSpeed);

            cs.SetFloat("minSlope", 0.0001f);
        }

        public void ApplyErosion(Vector2Int centre, ref float[,] noiseMap)
        {
            if (!settings.erosion.enabled || settings.erosionShader == null)
                return;

            int cellCount = mapChunkSize * mapChunkSize;

            EnsureErosionBuffers(cellCount, droplets);
            FlattenNoiseMap(ref noiseMap);
            GenerateDropletPositions(centre, droplets);

            erosionSourceMapBuffer.SetData(erosionSourceMap);

            Array.Clear(erosionDeltaMap, 0, cellCount);
            erosionDeltaBuffer.SetData(erosionDeltaMap);

            erosionStartPosBuffer.SetData(erosionStartPos);


            cs.SetBuffer(erosionKernel, "sourceMap", erosionSourceMapBuffer);
            cs.SetBuffer(erosionKernel, "erosionDelta", erosionDeltaBuffer);
            cs.SetBuffer(erosionKernel, "startPos", erosionStartPosBuffer);

            int groups = Mathf.CeilToInt(droplets / 64f);

            cs.Dispatch(erosionKernel, groups, 1, 1);

            erosionDeltaBuffer.GetData(erosionDeltaMap);

            ApplyErosionDelta(ref noiseMap);
        }

        private void EnsureErosionBuffers(int cellCount, int dropletCount)
        {
            bool recreateMaps =
                erosionSourceMapBuffer == null ||
                erosionDeltaBuffer == null ||
                cachedCellCount != cellCount;

            if (recreateMaps)
            {
                erosionSourceMapBuffer?.Dispose();
                erosionDeltaBuffer?.Dispose();

                erosionSourceMapBuffer = new ComputeBuffer(cellCount, sizeof(float));
                erosionDeltaBuffer     = new ComputeBuffer(cellCount, sizeof(float));

                erosionSourceMap = new float[cellCount];
                erosionDeltaMap  = new float[cellCount];

                cachedCellCount = cellCount;
            }

            if (erosionStartPosBuffer == null || cachedDropletCount != dropletCount)
            {
                erosionStartPosBuffer?.Dispose();

                erosionStartPosBuffer =
                    new ComputeBuffer(dropletCount, sizeof(float) * 2);

                erosionStartPos = new Vector2[dropletCount];

                cachedDropletCount = dropletCount;
            }

            if (erosionKernel < 0)
            {
                erosionKernel =
                    settings.erosionShader.FindKernel("CSMain");
            }
        }

        private void FlattenNoiseMap(ref float[,] noiseMap)
        {
            int idx = 0;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    erosionSourceMap[idx++] = noiseMap[x, y];
                }
            }
        }

        private void ApplyErosionDelta(ref float[,] noiseMap)
        {
            int idx = 0;

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    noiseMap[x, y] = Mathf.Clamp01(
                        noiseMap[x, y] + erosionDeltaMap[idx++]);
                }
            }
        }

        private void GenerateDropletPositions(
            Vector2Int centre,
            int dropletCount)
        {
            int rngSeed =
                settings.seed ^
                (centre.x * 73856093) ^
                (centre.y * 19349663);

            FastRandom rng = new(rngSeed);

            const int margin = 2;

            int min = margin;
            int max = mapChunkSize - margin;

            for (int i = 0; i < dropletCount; i++)
            {
                erosionStartPos[i] = new Vector2(
                    rng.Range(min, max),
                    rng.Range(min, max));
            }
        }

        public void Dispose()
        {
            erosionSourceMapBuffer?.Dispose();
            erosionDeltaBuffer?.Dispose();
            erosionStartPosBuffer?.Dispose();

            erosionSourceMapBuffer = null;
            erosionDeltaBuffer     = null;
            erosionStartPosBuffer  = null;

            erosionSourceMap = null;
            erosionDeltaMap  = null;
            erosionStartPos  = null;
        }
    }
}