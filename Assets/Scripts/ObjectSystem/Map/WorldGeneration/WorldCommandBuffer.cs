using System;
using Unity.Collections;
using UnityEngine;

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
}
