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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WorldToChunk(long worldPos) =>
            Position2Int.Pack(Position2Int.GetX(worldPos) >> ChunkShift, Position2Int.GetY(worldPos) >> ChunkShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldToLocalIdx(long worldPos) =>
            CellToLocalIdx(Position2Int.GetX(worldPos) >> CellShift, Position2Int.GetY(worldPos) >> CellShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CellToChunk(int cellX, int cellZ) =>
            Position2Int.Pack(cellX >> RowShift, cellZ >> RowShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellToLocalIdx(int cellX, int cellZ) =>
            ((cellZ & RowMask) << RowShift) | (cellX & RowMask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkCoord(float world) => (int)math.floor(world * InvChunkSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WindowChunk(long center, int gx, int gz) =>
            Position2Int.Offset(center, gx - chunkScale, gz - chunkScale);

        public static Vector3 WindowOriginWorld(long center) => new(
            (Position2Int.GetX(center) - chunkScale) * chunkSize,
            0f,
            (Position2Int.GetY(center) - chunkScale) * chunkSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInWindow(long center, long chunk, int radius) =>
            math.abs(Position2Int.GetX(chunk) - Position2Int.GetX(center)) <= radius &&
            math.abs(Position2Int.GetY(chunk) - Position2Int.GetY(center)) <= radius;

        public bool IsInActiveWindow(long worldPos) =>
            hasWindow && IsInWindow(windowCenter, WorldToChunk(worldPos), chunkScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockOfChunkDelta(int d)
        {
            int a = d + chunkScale;
            return (a >= 0 ? a : a - WindowSide + 1) / WindowSide;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BlockCenterChunk(long center, int bx, int bz) =>
            Position2Int.Offset(center, bx * WindowSide, bz * WindowSide);

        public static int ViewRingOf(long center, ViewSector sector, int ringCount, long chunk)
        {
            int bx = BlockOfChunkDelta(Position2Int.GetX(chunk) - Position2Int.GetX(center));
            int bz = BlockOfChunkDelta(Position2Int.GetY(chunk) - Position2Int.GetY(center));
            int ring = ViewWedge.Ring(bx, bz);

            if (ring == 0) return 0;
            if (ring > ringCount) return -1;
            return ViewWedge.Contains(sector, bx, bz) ? ring : -1;
        }

        public static int SimRingOf(long center, int ringCount, long chunk)
        {
            int d = math.max(
                math.abs(Position2Int.GetX(chunk) - Position2Int.GetX(center)),
                math.abs(Position2Int.GetY(chunk) - Position2Int.GetY(center)));

            if (d <= chunkScale) return 0;

            int ring = (d - chunkScale + WindowSide - 1) / WindowSide;
            return ring > ringCount ? -1 : ring;
        }

        public int ViewRingOfChunk(long chunkKey) =>
            hasWindow ? ViewRingOf(windowCenter, sector, ViewRingCount, chunkKey) : -1;

        public int RingOfChunk(long chunkKey) =>
            hasWindow ? SimRingOf(windowCenter, NalRingCount, chunkKey) : -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(long chunkKey) => EnsureChunk(chunkKey, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(int x, int y) => EnsureChunk(Position2Int.Pack(x, y), 0);

        private ChunkData ChunkForCell(long chunkKey) =>
            mapData.TryGetValue(chunkKey, out ChunkData data) ? data : GetMapData(chunkKey);

        public Vector3 GetRealPosition(long pos) => new(
            Position2Int.GetX(pos),
            ChunkForCell(WorldToChunk(pos)).HeightRaw[WorldToLocalIdx(pos)],
            Position2Int.GetY(pos));
    }
}