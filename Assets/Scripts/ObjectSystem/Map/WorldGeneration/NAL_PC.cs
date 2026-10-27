using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class NalStats
    {
        public int Processed, DueBacklog;
        public int BornApplied, BornRejected;
        public int Deaths, RemoveNoEffect, StaleRemovals;
        public int QueueLength, QueueMax;
        public int RemoveQueueLength, RemoveQueueMax;
        public int AddQueueLength, AddQueueMax;
        public int BatchMs, BatchMsMax;
        public int TickMs;
    }

    public class NAL_PC
    {
        private readonly struct RemoveOp
        {
            public readonly Vector2Int Pos;
            public readonly int PrefabId;

            public RemoveOp(Vector2Int pos, int prefabId)
            {
                Pos = pos;
                PrefabId = prefabId;
            }
        }

        private readonly ObjectSystem objectSystem;
        private readonly MapGenerator generator;
        private readonly CancellationToken token;

        private readonly NalSchedule schedule;
        private readonly HashSet<long> queued = new();
        private readonly Queue<RemoveOp> removeQueue = new(64);
        private readonly Queue<Pair<Vector2Int, ObjectInfo>> addQueue = new(64);

        private readonly int applyIntervalMs;
        private readonly long reportIntervalTicks = 10 * Stopwatch.Frequency;
        private long lastReportTicks;
        public readonly NalStats Stats = new();
        private int deadChance;

        public NAL_PC(MapGenerator generator, ObjectSystem objectSystem, CancellationToken token, int applyIntervalMs, int scheduleCapacity)
        {
            this.generator = generator;
            this.objectSystem = objectSystem;
            this.token = token;
            this.applyIntervalMs = applyIntervalMs;
            schedule = new NalSchedule(scheduleCapacity);
        }

        public void Enqueue(Vector2Int pos, int prefabId) => Schedule(pos, prefabId, SimulationClock.TimeD);

        public void Clear()
        {
            schedule.Clear();
            queued.Clear();
        }

        private void Schedule(Vector2Int pos, int prefabId, double due)
        {
            long packed = Position2Int.Pack(pos);
            if (!queued.Add(packed)) return;

            schedule.Push(due, packed, prefabId);
            if (schedule.Count > Stats.QueueMax) Stats.QueueMax = schedule.Count;
        }

        private void Requeue(Vector2Int pos, int prefabId, double due)
        {
            if (generator.IsInActiveWindow(Position2Int.Pack(pos)))
                Schedule(pos, prefabId, due);
        }

        public async UniTaskVoid RunNAL()
        {
            await UniTask.Delay(10000);

            while (!token.IsCancellationRequested)
            {
                Debug.Assert(queued.Count == schedule.Count, "NAL: queued/schedule desync");

                if (schedule.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                    continue;
                }

                long start = Stopwatch.GetTimestamp();
                double now = SimulationClock.TimeD;
                deadChance = schedule.Count > 200 ? 10 : 0;

                int budget = generator.NalMaxDecisionsPerTick;
                while (budget > 0 && schedule.TryPopDue(now, out NalSchedule.Entry entry))
                {
                    budget--;
                    queued.Remove(entry.Pos);
                    Stats.Processed++;
                    if (!Decide(Position2Int.UnpackToVector(entry.Pos), entry.PrefabId, now))
                        Stats.StaleRemovals++;
                }

                Stats.DueBacklog = budget == 0 ? schedule.CountDue(now) : 0;
                Stats.TickMs = (int)((Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency);

                await UniTask.Yield();
            }
        }

        private double DelaySeconds(NAlInfo n) =>
            n != null
                ? math.max((int)n.delay, 1) * generator.NalSecondsPerDelayUnit
                : applyIntervalMs * 0.00001d;

        private bool Decide(Vector2Int current, int prefabId, double now)
        {
            if (!generator.TryGetObject(Position2Int.Pack(current), out ObjectInstInfo instInfo)) return false;
            if (instInfo.PrefabID != prefabId) return false;

            ObjectInfo info = objectSystem.GetInfo(instInfo.PrefabID);
            if (info == null) return false;

            ObjectInfo next = info.EvolutionStep;

            if (info.BehaviourType == BehaviourType.GROW)
            {
                if (next == null)
                {
                    if (Extension.ComparePercent(25))
                        removeQueue.Enqueue(new RemoveOp(current, instInfo.PrefabID));
                    else
                        Requeue(current, prefabId, now + DelaySeconds(info.NalInfo));
                    return true;
                }

                removeQueue.Enqueue(new RemoveOp(current, instInfo.PrefabID));
                addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, next));
                return true;
            }

            bool close = false;
            for (int x = -MapGenerator.scale; x <= MapGenerator.scale; x++)
                for (int y = -MapGenerator.scale; y <= MapGenerator.scale; y++)
                    if ((x != 0 || y != 0) && generator.ContainsObject(Position2Int.Pack(current + new Vector2Int(x, y))))
                        close = true;

            NAlInfo n = info.NalInfo;
            if (n == null) return true;

            if (Extension.ComparePercent(n.chance / 2 + deadChance) || close)
            {
                removeQueue.Enqueue(new RemoveOp(current, instInfo.PrefabID));
                SpreadPattern pattern = info.OnDeathPattern;
                if (pattern != null)
                {
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, pattern.main));
                    foreach (var other in pattern.other)
                    {
                        Vector2Int p = Extension.GetRandomPointAround(current, pattern.factor);
                        addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(p, other));
                    }
                }
                return true;
            }

            if (!generator.IsHeightIsLiveAble(Position2Int.Pack(current))) return true;

            int attempts = n.attempt;
            while (attempts-- > 0)
            {
                if (next != null && Extension.ComparePercent(n.chance))
                {
                    Vector2Int p = Extension.GetRandomPointAround(current, n.distance);
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(p, next));
                }
            }

            Requeue(current, prefabId, now + DelaySeconds(n));
            return true;
        }

        public async UniTaskVoid RunUpdate()
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(applyIntervalMs, cancellationToken: token);

                long start = Stopwatch.GetTimestamp();

                Stats.RemoveQueueLength = removeQueue.Count;
                Stats.AddQueueLength = addQueue.Count;
                if (removeQueue.Count > Stats.RemoveQueueMax) Stats.RemoveQueueMax = removeQueue.Count;
                if (addQueue.Count > Stats.AddQueueMax) Stats.AddQueueMax = addQueue.Count;

                while (removeQueue.Count > 0)
                {
                    RemoveOp op = removeQueue.Dequeue();
                    long pos = Position2Int.Pack(op.Pos);

                    if (!generator.TryGetObject(pos, out ObjectInstInfo current) || current.PrefabID != op.PrefabId)
                    {
                        Stats.StaleRemovals++;
                        continue;
                    }

                    if (generator.RemoveObject(pos)) Stats.Deaths++;
                    else Stats.RemoveNoEffect++;
                }

                while (addQueue.Count > 0)
                {
                    var item = addQueue.Dequeue();
                    if (item.Second == null)
                    {
                        Stats.BornRejected++;
                        continue;
                    }

                    long pos = Position2Int.Pack(item.First);
                    if (generator.TryAddObject(
                            pos,
                            generator.GetRealPosition(pos),
                            item.Second.Id,
                            item.Second.DefaultAmount))
                    {
                        Stats.BornApplied++;
                        if (item.Second.HasLifecycle) Requeue(item.First, item.Second.Id, SimulationClock.TimeD);
                    }
                    else
                    {
                        Stats.BornRejected++;
                    }
                }

                generator.ExtraUpdate();

                Debug.Assert(removeQueue.Count == 0 && addQueue.Count == 0, "NAL: apply queues not drained");
                Debug.Assert(queued.Count == schedule.Count, "NAL: queued/schedule desync");

                long end = Stopwatch.GetTimestamp();
                Stats.BatchMs = (int)((end - start) * 1000 / Stopwatch.Frequency);
                if (Stats.BatchMs > Stats.BatchMsMax) Stats.BatchMsMax = Stats.BatchMs;
                Stats.QueueLength = schedule.Count;

                if (end - lastReportTicks >= reportIntervalTicks)
                {
                    lastReportTicks = end;
                    Report();
                }
            }
        }

        private void Report()
        {
            NalStats s = Stats;
            RavineLog.Info(
                $"[NAL] processed={s.Processed} dueBacklog={s.DueBacklog} born={s.BornApplied}/{s.BornRejected} " +
                $"deaths={s.Deaths} removeNoEffect={s.RemoveNoEffect} stale={s.StaleRemovals} " +
                $"queue={s.QueueLength}/{s.QueueMax} removeQ={s.RemoveQueueLength}/{s.RemoveQueueMax} " +
                $"addQ={s.AddQueueLength}/{s.AddQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax} tickMs={s.TickMs}");
        }
    }
}
