using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class NalStats
    {
        public int Processed, Skipped;
        public int BornApplied, BornRejected;
        public int Deaths, RemoveNoEffect, StaleRemovals;
        public int QueueLength, QueueMax;
        public int RemoveQueueLength, RemoveQueueMax;
        public int AddQueueLength, AddQueueMax;
        public int BatchMs, BatchMsMax;
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

        private readonly Queue<Vector2Int> nalQueue = new(128);
        private readonly HashSet<long> queued = new();
        private readonly Queue<RemoveOp> removeQueue = new(64);
        private readonly Queue<Pair<Vector2Int, ObjectInfo>> addQueue = new(64);

        private readonly int step = 10;
        private readonly int applyIntervalMs;
        private readonly long reportIntervalTicks = 10 * Stopwatch.Frequency;
        private long lastReportTicks;
        public readonly NalStats Stats = new();
        private int deadChance;
        private int cycle;

        public NAL_PC(MapGenerator generator, ObjectSystem objectSystem, CancellationToken token, int applyIntervalMs)
        {
            this.generator = generator;
            this.objectSystem = objectSystem;
            this.token = token;
            this.applyIntervalMs = applyIntervalMs;
        }

        public void Enqueue(Vector2Int pos)
        {
            if (!queued.Add(Position2Int.Pack(pos))) return;

            nalQueue.Enqueue(pos);
            if (nalQueue.Count > Stats.QueueMax) Stats.QueueMax = nalQueue.Count;
        }

        public void Clear()
        {
            nalQueue.Clear();
            queued.Clear();
        }

        private Vector2Int Dequeue()
        {
            Vector2Int pos = nalQueue.Dequeue();
            queued.Remove(Position2Int.Pack(pos));
            return pos;
        }

        private void Requeue(Vector2Int pos)
        {
            if (generator.IsInActiveWindow(Position2Int.Pack(pos)))
                Enqueue(pos);
        }

        public async UniTaskVoid RunNAL()
        {
            await UniTask.Delay(10000);

            while (!token.IsCancellationRequested)
            {
                cycle++;
                Debug.Assert(queued.Count == nalQueue.Count, "NAL: queued/nalQueue desync");

                if (nalQueue.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                    continue;
                }

                bool skip = nalQueue.Count > 100
                            ? cycle % (step * step) != 0
                            : cycle % step != 0;

                if (skip)
                {
                    Stats.Skipped++;
                    Requeue(Dequeue());
                    await UniTask.Delay(100, cancellationToken: token);
                    continue;
                }

                deadChance = nalQueue.Count > 200 ? 10 : 0;

                Vector2Int current = Dequeue();
                Stats.Processed++;
                ProcessNAL(current).Forget();

                await UniTask.Yield();
            }
        }

        private async UniTaskVoid ProcessNAL(Vector2Int current)
        {
            if (!generator.TryGetObject(Position2Int.Pack(current), out ObjectInstInfo instInfo)) return;

            ObjectInfo info = objectSystem.GetInfo(instInfo.PrefabID);
            if (info == null) return;

            ObjectInfo next = info.EvolutionStep;

            if (info.BehaviourType == BehaviourType.GROW)
            {
                if (next == null)
                {
                    if (Extension.ComparePercent(25))
                        removeQueue.Enqueue(new RemoveOp(current, instInfo.PrefabID));
                    else
                        Requeue(current);
                    return;
                }

                removeQueue.Enqueue(new RemoveOp(current, instInfo.PrefabID));
                addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, next));
                return;
            }

            bool close = false;
            for (int x = -MapGenerator.scale; x <= MapGenerator.scale; x++)
                for (int y = -MapGenerator.scale; y <= MapGenerator.scale; y++)
                    if ((x != 0 || y != 0) && generator.ContainsObject(Position2Int.Pack(current + new Vector2Int(x, y))))
                        close = true;

            NAlInfo n = info.NalInfo;
            if (n == null) return;

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
                return;
            }

            if (!generator.IsHeightIsLiveAble(Position2Int.Pack(current))) return;

            int attempts = n.attempt;
            while (attempts-- > 0)
            {
                if (next != null && Extension.ComparePercent(n.chance))
                {
                    Vector2Int p = Extension.GetRandomPointAround(current, n.distance);
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(p, next));
                }
                await UniTask.Delay(10, cancellationToken: token);
            }

            await UniTask.Delay(10 * n.delay, cancellationToken: token);
            Requeue(current);
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
                        if (item.Second.HasLifecycle) Requeue(item.First);
                    }
                    else
                    {
                        Stats.BornRejected++;
                    }
                }

                generator.ExtraUpdate();

                Debug.Assert(removeQueue.Count == 0 && addQueue.Count == 0, "NAL: apply queues not drained");
                Debug.Assert(queued.Count == nalQueue.Count, "NAL: queued/nalQueue desync");

                long end = Stopwatch.GetTimestamp();
                Stats.BatchMs = (int)((end - start) * 1000 / Stopwatch.Frequency);
                if (Stats.BatchMs > Stats.BatchMsMax) Stats.BatchMsMax = Stats.BatchMs;
                Stats.QueueLength = nalQueue.Count;

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
                $"[NAL] processed={s.Processed} skipped={s.Skipped} born={s.BornApplied}/{s.BornRejected} " +
                $"deaths={s.Deaths} removeNoEffect={s.RemoveNoEffect} stale={s.StaleRemovals} " +
                $"queue={s.QueueLength}/{s.QueueMax} removeQ={s.RemoveQueueLength}/{s.RemoveQueueMax} " +
                $"addQ={s.AddQueueLength}/{s.AddQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax}");
        }
    }
}
