using System;
using Unity.Collections;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public struct WorldCommand
    {
        public const byte Remove = 0;
        public const byte Add = 1;
        public const int NoExpectation = -1;
        public const int NoStamp = 0;

        public byte Kind;
        public long Pos;
        public int PrefabId;
        public int Amount;
        public int ExpectedPrefabId;
        public int ExpectedStamp;
        public byte Levels;
        public byte BaseLevel;
    }

    public sealed class WorldCommandStats
    {
        public int AddApplied, AddRejected;
        public int RemoveApplied, RemoveStale, RemoveNoEffect;
    }

    public interface IWorldCommandTarget
    {
        bool TryGetObject(long worldPos, out ObjectInstInfo info);
        bool RemoveObject(long worldPos);
        bool TryAddObject(long worldPos, Vector3 realPos, int prefabID, int amount, ReadOnlySpan<Vector2Int> additionalWorldCells, int levels);
        Vector3 GetRealPosition(long worldPos);

        bool TryGetObject(long worldPos, int level, out ObjectInstInfo info)
        {
            if (level == 0) return TryGetObject(worldPos, out info);
            info = default;
            return false;
        }

        bool RemoveObject(long worldPos, int level) => level == 0 && RemoveObject(worldPos);

        bool TryAddObject(long worldPos, Vector3 realPos, int prefabID, int amount, ReadOnlySpan<Vector2Int> additionalWorldCells, int levels, int baseLevel) =>
            baseLevel == 0 && TryAddObject(worldPos, realPos, prefabID, amount, additionalWorldCells, levels);
    }

    public sealed class WorldCommandBuffer
    {
        private const int CompactThreshold = 1024;

        private struct PendingCell
        {
            public int Count;
            public int Stamp;
        }

        private NativeList<WorldCommand> commands;
        private NativeHashMap<long, PendingCell> pendingRemoves;
        private int head;

        public WorldCommandBuffer(int capacity)
        {
            commands = new NativeList<WorldCommand>(capacity, Allocator.Persistent);
            pendingRemoves = new NativeHashMap<long, PendingCell>(capacity, Allocator.Persistent);
        }

        public int Count => commands.Length - head;
        public bool IsCreated => commands.IsCreated;

        public void Push(in WorldCommand command)
        {
            if (command.Kind != WorldCommand.Remove)
            {
                commands.Add(command);
                return;
            }

            long cell = CellKey(command.Pos);
            if (!pendingRemoves.TryGetValue(cell, out PendingCell pending)) pending.Stamp = 1;
            pending.Count++;
            pendingRemoves[cell] = pending;

            WorldCommand stamped = command;
            stamped.ExpectedStamp = pending.Stamp;
            commands.Add(stamped);
        }

        public bool HasPendingRemove(long worldPos) => pendingRemoves.ContainsKey(CellKey(worldPos));

        public bool HasAnyPendingRemove => pendingRemoves.IsCreated && !pendingRemoves.IsEmpty;

        public void Touch(long worldPos) => TouchCell(CellKey(worldPos));

        public void TouchCell(long cell)
        {
            if (!HasAnyPendingRemove || !pendingRemoves.TryGetValue(cell, out PendingCell pending)) return;
            pending.Stamp++;
            pendingRemoves[cell] = pending;
        }

        private bool StampMatches(long worldPos, int stamp) =>
            pendingRemoves.TryGetValue(CellKey(worldPos), out PendingCell pending) && pending.Stamp == stamp;

        private static long CellKey(long worldPos) =>
            Position2Int.Pack(Position2Int.GetX(worldPos) >> MapGenerator.CellShift, Position2Int.GetY(worldPos) >> MapGenerator.CellShift);

        private void ReleasePendingRemove(long worldPos)
        {
            long cell = CellKey(worldPos);
            if (!pendingRemoves.TryGetValue(cell, out PendingCell pending)) return;

            if (pending.Count <= 1) pendingRemoves.Remove(cell);
            else
            {
                pending.Count--;
                pendingRemoves[cell] = pending;
            }
        }

        public int Apply(IWorldCommandTarget target, long deadline, WorldCommandStats stats)
        {
            int processed = 0;
            while (head < commands.Length)
            {
                WorldCommand command = commands[head++];
                ApplyOne(target, in command, stats, this);
                if (command.Kind == WorldCommand.Remove) ReleasePendingRemove(command.Pos);
                processed++;

                if (FrameBudget.Expired(deadline)) break;
            }

            if (head == commands.Length)
            {
                commands.Clear();
                head = 0;
            }
            else if (head >= CompactThreshold && head * 2 >= commands.Length)
            {
                commands.RemoveRange(0, head);
                head = 0;
            }

            return processed;
        }

        public static bool ApplyOne(IWorldCommandTarget target, in WorldCommand command, WorldCommandStats stats, WorldCommandBuffer stamps = null)
        {
            if (command.Kind == WorldCommand.Remove)
            {
                if (command.ExpectedStamp != WorldCommand.NoStamp
                    && (stamps == null || !stamps.StampMatches(command.Pos, command.ExpectedStamp)))
                {
                    stats.RemoveStale++;
                    return false;
                }

                if (command.ExpectedPrefabId != WorldCommand.NoExpectation
                    && (!target.TryGetObject(command.Pos, command.BaseLevel, out ObjectInstInfo current) || current.PrefabID != command.ExpectedPrefabId))
                {
                    stats.RemoveStale++;
                    return false;
                }

                if (target.RemoveObject(command.Pos, command.BaseLevel))
                {
                    stats.RemoveApplied++;
                    return true;
                }

                stats.RemoveNoEffect++;
                return false;
            }

            if (target.TryAddObject(
                    command.Pos,
                    target.GetRealPosition(command.Pos),
                    command.PrefabId,
                    command.Amount,
                    default,
                    Math.Max((int)command.Levels, 1),
                    command.BaseLevel))
            {
                stats.AddApplied++;
                return true;
            }

            stats.AddRejected++;
            return false;
        }

        public void Dispose()
        {
            if (commands.IsCreated) commands.Dispose();
            if (pendingRemoves.IsCreated) pendingRemoves.Dispose();
        }
    }

    public enum WorldEditResult : byte
    {
        Ok = 0,
        NoObject = 1,
        NotPickable = 2,
        UnknownObject = 3,
        NotPlaceable = 4,
        Rejected = 5,
        Refused = 6,
        NotOwned = 7
    }

    public static class WorldEdits
    {
        public static WorldEditResult TryPickUp(MapGenerator generator, ObjectSystem objects, long pos, out int prefabId, out int amount) =>
            TryPickUp(generator, objects, pos, 0, out prefabId, out amount);

        public static WorldEditResult TryPickUp(MapGenerator generator, ObjectSystem objects, long pos, int level, out int prefabId, out int amount)
        {
            prefabId = ObjectInstInfo.PlaceholderId;
            amount = 0;

            if ((uint)level >= (uint)ObjectInfo.MaxLevels) return WorldEditResult.Refused;
            if (!generator.TryGetObject(pos, level, out ObjectInstInfo inst)) return WorldEditResult.NoObject;

            ObjectInfo info = objects.GetInfo(inst.PrefabID);
            if (info == null || info.InventoryItemInfo == null) return WorldEditResult.NotPickable;

            if (!ApplyRemove(generator, pos, inst.PrefabID, level)) return WorldEditResult.Rejected;

            prefabId = inst.PrefabID;
            amount = inst.Amount;
            Spread(generator, pos, info.OnPickUpPattern);
            generator.ExtraUpdate();
            return WorldEditResult.Ok;
        }

        public static WorldEditResult TryPlace(MapGenerator generator, ObjectSystem objects, long pos, int objectId, out PlacementResult placement) =>
            TryPlace(generator, objects, pos, 0, objectId, out placement);

        public static WorldEditResult TryPlace(MapGenerator generator, ObjectSystem objects, long pos, int level, int objectId, out PlacementResult placement)
        {
            placement = PlacementResult.Ok;
            ObjectInfo info = objects.GetInfo(objectId);
            if (info == null) return WorldEditResult.UnknownObject;
            if (info.InventoryItemInfo == null || !info.InventoryItemInfo.isPlaceable) return WorldEditResult.NotPlaceable;

            int levels = Math.Max(info.Levels, 1);
            if (level < 0 || level > ObjectInfo.MaxLevels - levels)
            {
                placement = PlacementResult.InvalidLevels;
                return WorldEditResult.Refused;
            }

            placement = generator.TryPlaceSnapped(pos, info, 1, level);
            if (placement == PlacementResult.Ok) generator.ExtraUpdate();

            return placement == PlacementResult.Ok ? WorldEditResult.Ok : WorldEditResult.Rejected;
        }

        public static void MirrorPickUp(MapGenerator generator, long pos, int prefabId) =>
            MirrorPickUp(generator, pos, 0, prefabId);

        public static void MirrorPickUp(MapGenerator generator, long pos, int level, int prefabId)
        {
            ApplyRemove(generator, pos, prefabId, level);
            generator.ExtraUpdate();
        }

        public static void MirrorPlace(MapGenerator generator, ObjectSystem objects, long pos, int objectId) =>
            MirrorPlace(generator, objects, pos, 0, objectId);

        public static void MirrorPlace(MapGenerator generator, ObjectSystem objects, long pos, int level, int objectId)
        {
            ObjectInfo info = objects.GetInfo(objectId);
            if (info == null) return;
            generator.TryPlaceSnapped(pos, info, 1, level);
            generator.ExtraUpdate();
        }

        private static bool ApplyRemove(MapGenerator generator, long pos, int prefabId, int level)
        {
            var remove = new WorldCommand
            {
                Kind = WorldCommand.Remove,
                Pos = pos,
                PrefabId = prefabId,
                ExpectedPrefabId = prefabId,
                BaseLevel = (byte)level
            };
            return generator.ApplyImmediate(in remove);
        }

        private static bool ApplyAdd(MapGenerator generator, long pos, ObjectInfo info, int amount, int baseLevel)
        {
            var add = new WorldCommand
            {
                Kind = WorldCommand.Add,
                Pos = pos,
                PrefabId = info.Id,
                Amount = amount,
                ExpectedPrefabId = WorldCommand.NoExpectation,
                Levels = (byte)Math.Max(info.Levels, 1),
                BaseLevel = (byte)baseLevel
            };
            return generator.ApplyImmediate(in add);
        }

        private static void Spread(MapGenerator generator, long pos, SpreadPattern pattern)
        {
            if (pattern == null) return;

            if (pattern.main != null) ApplyAdd(generator, pos, pattern.main, pattern.main.DefaultAmount, 0);
            if (pattern.other == null) return;

            Vector2Int center = Position2Int.UnpackToVector(pos);
            for (int i = 0; i < pattern.other.Length; i++)
            {
                ObjectInfo other = pattern.other[i];
                if (other == null) continue;
                long point = Position2Int.Pack(Extension.GetRandomPointAround(center, pattern.factor));
                ApplyAdd(generator, point, other, other.DefaultAmount, 0);
            }
        }
    }
}
