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
        private void MoveWindow(long center)
        {
            windowCenter = center;
            hasWindow = true;

            int streamedBefore = streamedChunks;
            EnsureRegion(center, chunkScale, 0, FrameBudget.Unlimited);
            EnsureRegion(center, HeightWindow.Radius, 1, FrameBudget.Unlimited);
            if (randomTicks != null) randomTicks.Stats.MoveWindowChunks += streamedChunks - streamedBefore;

            for (int i = 0; i < endless.Length; i++)
                endless[i]?.UpdateChunk(center);

            onUpdate?.Invoke(center);

            if (grassSystem != null)
                grassSystem.UpdateGrassPlacement(this, center);

            evictionPending = true;
            RestartSimSupply();
        }

        private void RestartSimSupply()
        {
            simPending = lodRingCount > 0 && NalRingCount > 0;
            simShell = 0;
            simStep = 0;
        }

        private void SupplySimSquare(long deadline)
        {
            int reach = NalReach;
            int cx = Position2Int.GetX(windowCenter);
            int cz = Position2Int.GetY(windowCenter);

            while (simShell <= reach)
            {
                int d = simShell;
                int perimeter = d == 0 ? 1 : 8 * d;
                int ring = d <= chunkScale ? 0 : (d - chunkScale + WindowSide - 1) / WindowSide;

                while (simStep < perimeter)
                {
                    int k = simStep;
                    int x, z;
                    if (d == 0) { x = 0; z = 0; }
                    else if (k <= 2 * d) { x = -d + k; z = -d; }
                    else if (k <= 4 * d) { x = d; z = -d + (k - 2 * d); }
                    else if (k <= 6 * d) { x = d - (k - 4 * d); z = d; }
                    else { x = -d; z = d - (k - 6 * d); }

                    long key = Position2Int.Pack(cx + x, cz + z);
                    if (!mapData.TryGetValue(key, out ChunkData cd) || cd.DetailLevel > ring)
                    {
                        if (FrameBudget.Expired(deadline)) return;
                        EnsureChunk(key, ring);
                    }

                    simStep++;
                }

                simShell++;
                simStep = 0;
            }

            simPending = false;
            simShell = 0;
        }

        private void SupplyLookahead(long deadline)
        {
            float fraction = Settings.lookaheadEdgeFraction;
            if (!hasWindow || viewer == null || !(fraction > 0f))
            {
                lookaheadDx = 0;
                lookaheadDz = 0;
                return;
            }

            Vector3 p = viewer.position + viewerOffset;
            float u = p.x * InvChunkSize;
            float v = p.z * InvChunkSize;
            float fu = u - math.floor(u);
            float fv = v - math.floor(v);
            int dx = fu < fraction ? -1 : fu > 1f - fraction ? 1 : 0;
            int dz = fv < fraction ? -1 : fv > 1f - fraction ? 1 : 0;

            if (dx != lookaheadDx || dz != lookaheadDz || windowCenter != lookaheadWindow)
            {
                lookaheadDx = dx;
                lookaheadDz = dz;
                lookaheadWindow = windowCenter;
                lookaheadVersion = -1;
            }

            if (dx == 0 && dz == 0) return;
            if (lookaheadVersion == chunkSetVersion) return;

            int before = streamedChunks;
            bool complete = true;
            if (dx != 0) complete &= EnsureShifted(dx, 0, deadline);
            if (dz != 0) complete &= EnsureShifted(0, dz, deadline);
            if (dx != 0 && dz != 0) complete &= EnsureShifted(dx, dz, deadline);

            if (randomTicks != null) randomTicks.Stats.LookaheadChunks += streamedChunks - before;
            if (complete) lookaheadVersion = chunkSetVersion;
        }

        private bool EnsureShifted(int dx, int dz, long deadline)
        {
            long shifted = Position2Int.Offset(windowCenter, dx, dz);
            bool near = EnsureRegion(shifted, chunkScale, 0, deadline);
            bool far = EnsureRegion(shifted, HeightWindow.Radius, 1, deadline);
            return near && far;
        }

        private bool IsLookaheadRetained(long chunkKey)
        {
            if (lookaheadDx == 0 && lookaheadDz == 0) return false;

            int r = HeightWindow.Radius;
            int x = Position2Int.GetX(chunkKey) - Position2Int.GetX(windowCenter);
            int z = Position2Int.GetY(chunkKey) - Position2Int.GetY(windowCenter);
            return x >= math.min(0, lookaheadDx) - r && x <= math.max(0, lookaheadDx) + r
                && z >= math.min(0, lookaheadDz) - r && z <= math.max(0, lookaheadDz) + r;
        }

        private long ComputeWindowCenter()
        {
            if (viewer == null) return hasWindow ? windowCenter : 0L;

            Vector3 p = viewer.position + viewerOffset;
            return Position2Int.Pack(ChunkCoord(p.x), ChunkCoord(p.z));
        }

        private void EvictChunks()
        {
            evictionPending = false;
            nextEvictionScan = Time.unscaledTime + Settings.evictionScanInterval;
            if (!hasWindow) return;

            int idleFrames = Settings.evictionIdleFrames;
            int n = 0;

            foreach (var kv in mapData)
            {
                ChunkData cd = kv.Value;
                if (cd.NeedsSave || frame - cd.LastAccessFrame < idleFrames || IsRetained(kv.Key))
                    continue;

                if (n == evictBuffer.Length)
                    Array.Resize(ref evictBuffer, n * 2);
                evictBuffer[n++] = kv.Key;
            }

            for (int i = 0; i < n; i++)
            {
                if (mapData.TryGetValue(evictBuffer[i], out ChunkData cd))
                    ReleaseChunk(evictBuffer[i], cd);
            }

            if (n > 0) RestartSimSupply();
        }

        private bool IsRetained(long chunkKey)
        {
            int hysteresis = Settings.evictionHysteresis;
            int d = math.max(
                math.abs(Position2Int.GetX(chunkKey) - Position2Int.GetX(windowCenter)),
                math.abs(Position2Int.GetY(chunkKey) - Position2Int.GetY(windowCenter)));

            int near = math.max(Settings.retainRadius, HeightWindow.Radius) + hysteresis;
            if (d <= near || IsLookaheadRetained(chunkKey)) return true;
            return lodRingCount > 0 && d <= NalReach + hysteresis;
        }
    }
}