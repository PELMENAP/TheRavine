using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;
using TheRavine.Events;
using TheRavine.ViewControl;
using R3;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    using TheRavine.Base;
    using Unity.Mathematics;

    public partial class MapGenerator
    {
        public const float MaxWalkableSlope = 70f;

        public void BindWaterLevel(float level) => WaterLevel = level;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AboveWater(in RavineProfile ravine, float height, float z, float waterLevel)
            => height - ravine.Elevation(z) - waterLevel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WaterModifier(float aboveWater, float inWaterMul)
            => aboveWater < 0f ? inWaterMul : 1f;

        public bool IsHeightIsLiveAble(long position) =>
            AboveWater(Ravine, GetRealPosition(position).y, Position2Int.GetY(position), WaterLevel) > 0f;

        public bool IsWaterHeight(long position) =>
            AboveWater(Ravine, GetRealPosition(position).y, Position2Int.GetY(position), WaterLevel) < 0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSpeedModifier(float x, float z, float2 moveDirection)
            => SpeedFromNormal(SampleNormal(x, z), moveDirection);

        public static float SpeedFromNormal(float3 normal, float2 moveDirection)
        {
            float slopeAngle = math.degrees(math.acos(math.clamp(normal.y, -1f, 1f)));
            float2 uphill    = math.normalizesafe(new float2(-normal.x, -normal.z));
            float uphillDot  = math.dot(math.normalize(moveDirection), uphill);

            if (uphillDot > 0f && slopeAngle > MaxWalkableSlope) return 0f;

            float modifier = 1f - slopeAngle / MaxWalkableSlope;
            if (uphillDot < 0f)
                modifier = math.lerp(modifier, 1f, -uphillDot * 0.25f);

            return math.saturate(modifier);
        }

        public float3 SampleNormal(float wx, float wz)
            => NormalFromHeights(
                SampleHeightBilinear(wx - scale, wz),
                SampleHeightBilinear(wx + scale, wz),
                SampleHeightBilinear(wx, wz - scale),
                SampleHeightBilinear(wx, wz + scale));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalFromHeights(float hL, float hR, float hD, float hU)
            => math.normalize(new float3(hL - hR, scale * 2f, hD - hU));

        private struct ChunkObstacles : IObstacleLookup
        {
            public LongDictionary<ChunkData> Chunks;

            public bool TryGetObstacles(long chunkKey, out ObstacleChunk chunk)
            {
                if (Chunks.TryGetValue(chunkKey, out ChunkData data))
                {
                    chunk = data.ObstacleView();
                    return true;
                }
                chunk = default;
                return false;
            }
        }

        public bool ResolveObstacles(float2 origin, ref float2 position, float radius, float feetY, float headY, float stepUp, out float2 normal)
        {
            if (Collision == null)
            {
                normal = float2.zero;
                return false;
            }

            ObstacleSet set = Collision.Set;
            var lookup = new ChunkObstacles { Chunks = mapData };
            return ObstacleQuery.ResolveSwept(ref lookup, in set, origin, ref position, radius, feetY, headY, stepUp, out normal);
        }

        public bool SupportObstacles(float2 position, float radius, float feetY, float stepUp, out float top)
        {
            if (Collision == null)
            {
                top = 0f;
                return false;
            }

            ObstacleSet set = Collision.Set;
            var lookup = new ChunkObstacles { Chunks = mapData };
            return ObstacleQuery.Support(ref lookup, in set, position, radius, feetY, stepUp, out top);
        }

        public float SampleHeightBilinear(float wx, float wz)
        {
            TrySampleHeightBilinear(wx, wz, out float height);
            return height;
        }

        public bool TrySampleHeightBilinear(float wx, float wz, out float height)
        {
            float gx = wx * InvScale;
            float gz = wz * InvScale;
            float fx = math.floor(gx);
            float fz = math.floor(gz);
            int x0 = (int)fx;
            int z0 = (int)fz;

            if (!TryCellHeight(x0, z0, out float h00))
            {
                height = 0f;
                return false;
            }

            float h10 = TryCellHeight(x0 + 1, z0,     out float a) ? a : h00;
            float h01 = TryCellHeight(x0,     z0 + 1, out float b) ? b : h00;
            float h11 = TryCellHeight(x0 + 1, z0 + 1, out float c) ? c : h00;

            float tx = gx - fx;
            float tz = gz - fz;

            height = math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
            return true;
        }

        public bool TrySampleTerrainSurface(float2 position, out float height, out float2 gradient)
        {
            float2 grid = position * InvScale;
            float2 cell = math.floor(grid);
            float2 fraction = grid - cell;
            int x = (int)cell.x;
            int z = (int)cell.y;
            bool upper = fraction.y >= fraction.x;

            if (!TryCellHeight(x, z, out float bottomLeft) ||
                !TryCellHeight(x + 1, z + 1, out float topRight) ||
                !TryCellHeight(upper ? x : x + 1, upper ? z + 1 : z, out float corner))
            {
                height = 0f;
                gradient = float2.zero;
                return false;
            }

            float2 rise = upper
                ? new float2(topRight - corner, corner - bottomLeft)
                : new float2(corner - bottomLeft, topRight - corner);
            height = bottomLeft + math.dot(fraction, rise);
            gradient = rise * InvScale;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryCellHeight(int cellX, int cellZ, out float height)
        {
            long key = CellToChunk(cellX, cellZ);
            if (key != sampleKey)
            {
                if (!mapData.TryGetValue(key, out ChunkData cd))
                {
                    height = 0f;
                    return false;
                }
                sampleKey = key;
                sampleChunk = cd;
            }

            height = sampleChunk.HeightRaw[CellToLocalIdx(cellX, cellZ)];
            return true;
        }

        public bool SetTerrainHeight(long worldPos, float height)
        {
            long chunk = WorldToChunk(worldPos);
            if (!mapData.TryGetValue(chunk, out ChunkData cd)) return false;
            return SetTerrainHeight(chunk, cd, WorldToLocalIdx(worldPos), height);
        }

        private bool SetTerrainHeight(long chunk, ChunkData cd, int idx, float height)
        {
            if (!cd.SetHeight(idx, height)) return false;

            MarkChunkDirty(chunk);
            if (endless != null)
            {
                for (int i = 0; i < endless.Length; i++)
                    endless[i]?.OnTerrainChanged(chunk);
            }
            return true;
        }
    }
}