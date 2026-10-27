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

        public byte Kind;
        public long Pos;
        public int PrefabId;
        public int Amount;
        public int ExpectedPrefabId;
        public byte Levels;
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
    }

    public sealed class WorldCommandBuffer
    {
        private const int CompactThreshold = 1024;

        private NativeList<WorldCommand> commands;
        private NativeList<WorldCommand> appliedAdds;
        private int head;

        public WorldCommandBuffer(int capacity)
        {
            commands = new NativeList<WorldCommand>(capacity, Allocator.Persistent);
            appliedAdds = new NativeList<WorldCommand>(capacity, Allocator.Persistent);
        }

        public int Count => commands.Length - head;
        public bool IsCreated => commands.IsCreated;
        public NativeList<WorldCommand> AppliedAdds => appliedAdds;

        public void Push(in WorldCommand command) => commands.Add(command);

        public int Apply(IWorldCommandTarget target, long deadline, WorldCommandStats stats)
        {
            int processed = 0;
            while (head < commands.Length)
            {
                WorldCommand command = commands[head++];
                if (ApplyOne(target, in command, stats) && command.Kind == WorldCommand.Add)
                    appliedAdds.Add(command);
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

        public static bool ApplyOne(IWorldCommandTarget target, in WorldCommand command, WorldCommandStats stats)
        {
            if (command.Kind == WorldCommand.Remove)
            {
                if (command.ExpectedPrefabId != WorldCommand.NoExpectation
                    && (!target.TryGetObject(command.Pos, out ObjectInstInfo current) || current.PrefabID != command.ExpectedPrefabId))
                {
                    stats.RemoveStale++;
                    return false;
                }

                if (target.RemoveObject(command.Pos))
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
                    Math.Max((int)command.Levels, 1)))
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
            if (appliedAdds.IsCreated) appliedAdds.Dispose();
        }
    }

    public enum WorldEditResult : byte
    {
        Ok = 0,
        NoObject = 1,
        NotPickable = 2,
        UnknownObject = 3,
        NotPlaceable = 4,
        Occupied = 5,
        Rejected = 6
    }

    public static class WorldEdits
    {
        public static WorldEditResult TryPickUp(MapGenerator generator, ObjectSystem objects, long pos, out int prefabId, out int amount)
        {
            prefabId = ObjectInstInfo.PlaceholderId;
            amount = 0;

            if (!generator.TryGetObject(pos, out ObjectInstInfo inst)) return WorldEditResult.NoObject;

            ObjectInfo info = objects.GetInfo(inst.PrefabID);
            if (info == null || info.InventoryItemInfo == null) return WorldEditResult.NotPickable;

            if (!ApplyRemove(generator, pos, inst.PrefabID)) return WorldEditResult.Rejected;

            prefabId = inst.PrefabID;
            amount = inst.Amount;
            Spread(generator, pos, info.OnPickUpPattern);
            generator.ExtraUpdate();
            return WorldEditResult.Ok;
        }

        public static WorldEditResult TryPlace(MapGenerator generator, ObjectSystem objects, long pos, int objectId)
        {
            ObjectInfo info = objects.GetInfo(objectId);
            if (info == null) return WorldEditResult.UnknownObject;
            if (info.InventoryItemInfo == null || !info.InventoryItemInfo.isPlaceable || info.PlacementKind != PlacementKind.Surface2D)
                return WorldEditResult.NotPlaceable;
            if (generator.ContainsObject(pos)) return WorldEditResult.Occupied;

            bool ok = ApplyAdd(generator, pos, info, 1);
            generator.ExtraUpdate();
            return ok ? WorldEditResult.Ok : WorldEditResult.Rejected;
        }

        public static void MirrorPickUp(MapGenerator generator, long pos, int prefabId)
        {
            ApplyRemove(generator, pos, prefabId);
            generator.ExtraUpdate();
        }

        public static void MirrorPlace(MapGenerator generator, ObjectSystem objects, long pos, int objectId)
        {
            ObjectInfo info = objects.GetInfo(objectId);
            if (info == null) return;
            ApplyAdd(generator, pos, info, 1);
            generator.ExtraUpdate();
        }

        private static bool ApplyRemove(MapGenerator generator, long pos, int prefabId)
        {
            var remove = new WorldCommand
            {
                Kind = WorldCommand.Remove,
                Pos = pos,
                PrefabId = prefabId,
                ExpectedPrefabId = prefabId
            };
            return generator.ApplyImmediate(in remove);
        }

        private static bool ApplyAdd(MapGenerator generator, long pos, ObjectInfo info, int amount)
        {
            var add = new WorldCommand
            {
                Kind = WorldCommand.Add,
                Pos = pos,
                PrefabId = info.Id,
                Amount = amount,
                ExpectedPrefabId = WorldCommand.NoExpectation,
                Levels = (byte)Math.Max(info.Levels, 1)
            };
            return generator.ApplyImmediate(in add);
        }

        private static void Spread(MapGenerator generator, long pos, SpreadPattern pattern)
        {
            if (pattern == null) return;

            if (pattern.main != null) ApplyAdd(generator, pos, pattern.main, pattern.main.DefaultAmount);
            if (pattern.other == null) return;

            Vector2Int center = Position2Int.UnpackToVector(pos);
            for (int i = 0; i < pattern.other.Length; i++)
            {
                ObjectInfo other = pattern.other[i];
                if (other == null) continue;
                long point = Position2Int.Pack(Extension.GetRandomPointAround(center, pattern.factor));
                ApplyAdd(generator, point, other, other.DefaultAmount);
            }
        }
    }
}
