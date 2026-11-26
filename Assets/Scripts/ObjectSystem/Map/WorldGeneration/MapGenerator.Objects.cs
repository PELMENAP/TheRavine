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
        public bool TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells = default,
            int levels = 1,
            int baseLevel = 0) =>
            TryAddObjectEx(worldPos, realPos, prefabID, amount, additionalWorldCells, levels, baseLevel) == PlacementResult.Ok;

        bool IWorldCommandTarget.TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells,
            int levels) =>
            ((IWorldCommandTarget)this).TryAddObject(worldPos, realPos, prefabID, amount, additionalWorldCells, levels, 0);

        bool IWorldCommandTarget.TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells,
            int levels,
            int baseLevel)
        {
            ObjectInfo info = additionalWorldCells.IsEmpty && objectSystem != null ? objectSystem.GetInfo(prefabID) : null;
            return info != null
                ? TryAddFromInfo(worldPos, realPos, info, amount, levels, baseLevel) == PlacementResult.Ok
                : TryAddObjectEx(worldPos, realPos, prefabID, amount, additionalWorldCells, levels, baseLevel) == PlacementResult.Ok;
        }

        public PlacementResult TryAddFromInfo(long worldPos, Vector3 realPos, ObjectInfo info, int amount, int levels, int baseLevel = 0)
        {
            Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];
            int count = Footprint.Resolve(info, Position2Int.UnpackToVector(worldPos), cells);
            if (count > cells.Length)
            {
                RavineLog.Warning($"[MapGenerator] Too many secondary cells: {count} > {ObjectInstInfo.MaxSecondary}");
                return PlacementResult.TooManyCells;
            }

            return TryAddObjectEx(worldPos, realPos, info.Id, amount, cells[..count], levels, baseLevel);
        }

        public float PlacementY(long worldPos, ObjectInfo info, int baseLevel)
        {
            float top = GetRealPosition(worldPos).y;
            if (info != null)
            {
                Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];
                int count = Footprint.Resolve(info, Position2Int.UnpackToVector(worldPos), cells);
                if (count > cells.Length) count = 0;
                for (int i = 0; i < count; i++)
                    top = math.max(top, GetRealPosition(Position2Int.Pack(cells[i])).y);
            }

            return LevelSpace.SnapY(top, baseLevel, Settings.levelHeight);
        }

        public PlacementResult TryPlaceSnapped(long worldPos, ObjectInfo info, int amount, int baseLevel)
        {
            Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];
            int count = Footprint.Resolve(info, Position2Int.UnpackToVector(worldPos), cells);
            if (count > cells.Length)
            {
                RavineLog.Warning($"[MapGenerator] Too many secondary cells: {count} > {ObjectInstInfo.MaxSecondary}");
                return PlacementResult.TooManyCells;
            }

            float y = PlacementY(worldPos, info, baseLevel);
            Vector3 realPos = GetRealPosition(worldPos);
            realPos.y = y;

            PlacementResult result = TryAddObjectEx(worldPos, realPos, info.Id, amount, cells[..count], info.Levels, baseLevel);
            if (result != PlacementResult.Ok) return result;

            FillGround(worldPos, y);
            for (int i = 0; i < count; i++)
                FillGround(Position2Int.Pack(cells[i]), y);
            return PlacementResult.Ok;
        }

        private void FillGround(long worldPos, float objectY)
        {
            long chunk = WorldToChunk(worldPos);
            if (!mapData.TryGetValue(chunk, out ChunkData cd)) return;

            int idx = WorldToLocalIdx(worldPos);
            if (!LevelSpace.NeedsFill(objectY, cd.HeightRaw[idx], Settings.levelHeight)) return;

            SetTerrainHeight(chunk, cd, idx, objectY);
        }

        public PlacementResult TryAddObjectEx(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells = default,
            int levels = 1,
            int baseLevel = 0)
        {
            long chunk = WorldToChunk(worldPos);
            ChunkData cd = ChunkForCell(chunk);
            if (cd == null) return PlacementResult.NoChunk;

            int primaryIdx = WorldToLocalIdx(worldPos);

            int secCount = additionalWorldCells.Length;
            if (secCount > ObjectInstInfo.MaxSecondary)
            {
                RavineLog.Warning($"[MapGenerator] Too many secondary cells: {secCount} > {ObjectInstInfo.MaxSecondary}");
                return PlacementResult.TooManyCells;
            }

            Span<int> additionalLocalIdxs = stackalloc int[ObjectInstInfo.MaxSecondary];
            int unique = 0;

            for (int i = 0; i < secCount; i++)
            {
                long worldCell = Position2Int.Pack(additionalWorldCells[i]);

                if (WorldToChunk(worldCell) != chunk)
                {
                    RavineLog.Warning(
                        $"[MapGenerator] AdditionalCell {additionalWorldCells[i]} falls outside " +
                        $"primary chunk {chunk}. Cross-chunk multi-cell objects are not supported.");
                    return PlacementResult.CrossChunk;
                }

                int local = WorldToLocalIdx(worldCell);
                if (local == primaryIdx || additionalLocalIdxs[..unique].IndexOf(local) >= 0) continue;
                additionalLocalIdxs[unique++] = local;
            }

            var info = new ObjectInstInfo(realPos, prefabID, amount);
            if (!cd.TryAddObject(primaryIdx, in info, additionalLocalIdxs[..unique], levels, baseLevel, out PlacementResult result))
                return result;

            MarkChunkDirty(chunk);
            TouchEdited(worldPos, additionalWorldCells);
            return PlacementResult.Ok;
        }

        private void TouchEdited(long worldPos, ReadOnlySpan<Vector2Int> additionalWorldCells)
        {
            WorldCommandBuffer commands = Commands;
            if (commands == null || !commands.HasAnyPendingRemove) return;

            commands.Touch(worldPos);
            for (int i = 0; i < additionalWorldCells.Length; i++)
                commands.Touch(Position2Int.Pack(additionalWorldCells[i]));
        }

        public bool TryGetObject(long worldPos, out ObjectInstInfo info)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
            {
                info = default;
                return false;
            }

            return cd.TryGetObject(WorldToLocalIdx(worldPos), out info);
        }

        public bool TryGetObject(long worldPos, int level, out ObjectInstInfo info)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
            {
                info = default;
                return false;
            }

            return cd.TryGetObject(WorldToLocalIdx(worldPos), level, out info);
        }

        public bool RemoveObject(long worldPos) => RemoveObject(worldPos, 0);

        public bool RemoveObject(long worldPos, int level)
        {
            long chunk = WorldToChunk(worldPos);
            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            if (!cd.RemoveObject(WorldToLocalIdx(worldPos), level))
                return false;

            MarkChunkDirty(chunk);
            TouchEdited(worldPos, default);
            return true;
        }

        public bool ContainsObject(long worldPos)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
                return false;

            return cd.Occupancy[WorldToLocalIdx(worldPos)] != 0;
        }

        public bool IsOccupiedAt(long worldPos, int level)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
                return false;

            return cd.HasLevelAt(WorldToLocalIdx(worldPos), level);
        }

        public bool TryToAddPositionToChunk(long worldPos)
        {
            if (ContainsObject(worldPos))
                return false;

            long chunk = WorldToChunk(worldPos);
            ChunkData cd = GetMapData(chunk);

            var placeholder = new ObjectInstInfo(
                GetRealPosition(worldPos), ObjectInstInfo.PlaceholderId, 0);

            if (!cd.TryAddObject(WorldToLocalIdx(worldPos), in placeholder))
                return false;

            MarkChunkDirty(chunk);
            TouchEdited(worldPos, default);
            return true;
        }

        public void MarkChunkDirty(long chunkKey)
        {
            for (int i = 0; i < dirtyCount; i++)
                if (dirtyKeys[i] == chunkKey) return;

            if (dirtyCount == dirtyKeys.Length)
                Array.Resize(ref dirtyKeys, dirtyCount * 2);

            dirtyKeys[dirtyCount++] = chunkKey;
        }
    }
}