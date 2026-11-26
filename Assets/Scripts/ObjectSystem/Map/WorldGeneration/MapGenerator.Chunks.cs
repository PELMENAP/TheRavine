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
        public ChunkData EnsureChunk(long chunkKey, int ring)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData data))
            {
                if (data.DetailLevel > ring)
                {
                    GeneratorFor(chunkKey).RaiseDetail(data, chunkKey, ring);
                    streamedChunks++;
                }
            }
            else
            {
                data = chunkPool.Count > 0 ? chunkPool.Pop() : new ChunkData();
                GenerateChunk(chunkKey, data, ring);
                mapData[chunkKey] = data;
                chunkSetVersion++;
                streamedChunks++;
            }

            data.LastAccessFrame = frame;
            return data;
        }

        private ChunkGenerator GeneratorFor(long chunkKey)
        {
            if (plateauGenerator == null) return chunkGenerator;
            float centreZ = (Position2Int.GetY(chunkKey) + 0.5f) * chunkSize;
            return RavineProfile.IsPlateau(Ravine.Blend(centreZ)) ? plateauGenerator : chunkGenerator;
        }

        private void GenerateChunk(long chunkKey, ChunkData data, int ring)
        {
            ChunkGenerator generator = GeneratorFor(chunkKey);
            long worldTick = WorldTick;
            long epochTicks = tickRules.erosionEpochTicks;
            erosionBudget.LimitTicks = FrameBudget.TicksFromMs(tickRules.erosionEpochBudgetMs);

            int stored = chunkStore != null ? chunkStore.PeekErosionEpoch(chunkKey) : 0;
            if (stored <= 0)
            {
                int due = ErosionEpochsDue(0L, worldTick);
                int applied = generator.Generate(data, chunkKey, ring, 1, due, erosionBudget, out bool hit);
                data.LastTick = applied * epochTicks;
                RecordErosion(applied, hit);
                return;
            }

            generator.Generate(data, chunkKey, ring, stored, 0, erosionBudget, out _);
            data.LastTick = (stored - 1) * epochTicks;
            chunkStore.TryRestore(chunkKey, data, worldTick, objectSystem != null ? objectSystem.infoRegistry : null);

            if (data.LastTick > worldTick) data.LastTick = worldTick;
            int remaining = ErosionEpochsDue(data.LastTick, worldTick);
            int done = generator.CatchUpTerrain(data, chunkKey, remaining, erosionBudget, out bool budgetHit);
            data.LastTick += done * epochTicks;
            RecordErosion(done, budgetHit);
        }

        private int ErosionEpochsDue(long lastTick, long worldTick)
        {
            long elapsed = worldTick - lastTick;
            if (elapsed <= 0L) return 0;
            return (int)Math.Min(elapsed / tickRules.erosionEpochTicks, tickRules.erosionMaxEpochsPerEntry);
        }

        private void RecordErosion(int applied, bool budgetHit)
        {
            if (randomTicks == null) return;
            randomTicks.Stats.CatchUpEpochs += applied;
            if (budgetHit) randomTicks.Stats.CatchUpBudgetHits++;
        }

        public bool EnsureRegion(long center, int radius, int ring, long deadline)
        {
            bool complete = true;
            int cx = Position2Int.GetX(center);
            int cz = Position2Int.GetY(center);

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                long key = Position2Int.Pack(cx + dx, cz + dz);
                if (mapData.TryGetValue(key, out ChunkData cd) && cd.DetailLevel <= ring)
                {
                    cd.LastAccessFrame = frame;
                    continue;
                }

                if (FrameBudget.Expired(deadline))
                {
                    complete = false;
                    continue;
                }

                EnsureChunk(key, ring);
            }

            return complete;
        }

        public bool TryBuildHeightWindow(long center, out HeightWindow window)
        {
            window = default;
            int ox = Position2Int.GetX(center) - HeightWindow.Radius;
            int oz = Position2Int.GetY(center) - HeightWindow.Radius;

            for (int z = 0; z < HeightWindow.Side; z++)
            for (int x = 0; x < HeightWindow.Side; x++)
            {
                if (!mapData.TryGetValue(Position2Int.Pack(ox + x, oz + z), out ChunkData cd))
                    return false;
                window.Set(x, z, cd);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasChunk(long chunkKey) => mapData.ContainsKey(chunkKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetChunk(long chunkKey, out ChunkData data) =>
            mapData.TryGetValue(chunkKey, out data);

        public void TouchChunk(long chunkKey)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData cd)) cd.LastAccessFrame = frame;
        }

        public void EnsureWaterDistance(long chunkKey, ChunkData cd, float level, int ring)
        {
            bool stamped = cd.WaterRingStamp == ring && cd.WaterLevelStamp == level;
            if (stamped && cd.WaterHoodVersion == chunkSetVersion) return;

            int cx = Position2Int.GetX(chunkKey);
            int cz = Position2Int.GetY(chunkKey);
            int mask = 0;

            for (int sz = -1; sz <= 1; sz++)
            for (int sx = -1; sx <= 1; sx++)
            {
                ChunkData neighbor = null;
                if (sx == 0 && sz == 0) neighbor = cd;
                else mapData.TryGetValue(Position2Int.Pack(cx + sx, cz + sz), out neighbor);
                int slot = (sz + 1) * 3 + sx + 1;
                waterNeighborhood[slot] = neighbor;
                if (neighbor != null) mask |= 1 << slot;
            }

            cd.WaterHoodVersion = chunkSetVersion;
            if (stamped && cd.WaterHoodMask == mask) return;

            cd.WaterHoodMask = mask;
            chunkGenerator.BuildWaterDistance(chunkKey, cd, waterNeighborhood, level, ring);
        }

        public void RequestChunk(long chunkKey)
        {
            if (mapData.ContainsKey(chunkKey)) return;
            for (int i = 0; i < chunkRequestCount; i++)
                if (chunkRequests[i] == chunkKey) return;
            if (chunkRequestCount == chunkRequests.Length) return;
            chunkRequests[chunkRequestCount++] = chunkKey;
        }

        private void ProcessChunkRequests(long deadline)
        {
            int done = 0;
            while (done < chunkRequestCount)
            {
                if (done > 0 && FrameBudget.Expired(deadline)) break;
                long key = chunkRequests[done++];
                if (!mapData.ContainsKey(key)) EnsureChunk(key, 0);
            }

            if (done == 0) return;
            int left = chunkRequestCount - done;
            if (left > 0) Array.Copy(chunkRequests, done, chunkRequests, 0, left);
            chunkRequestCount = left;
        }

        public void RemoveChunk(long chunkKey) => UnloadChunk(chunkKey);

        public void UnloadChunk(long chunkKey)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData cd))
                ReleaseChunk(chunkKey, cd);
        }

        private void ReleaseChunk(long chunkKey, ChunkData cd)
        {
            randomTicks?.OnChunkReleased(chunkKey);
            mapData.Remove(chunkKey);
            chunkSetVersion++;
            UnloadStamp++;

            if (ReferenceEquals(sampleChunk, cd))
            {
                sampleChunk = null;
                sampleKey = long.MinValue;
            }

            if (chunkPool.Count >= Settings.chunkPoolCapacity)
            {
                cd.Dispose();
                return;
            }

            cd.ResetForReuse();
            chunkPool.Push(cd);
        }
    }
}