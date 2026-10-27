using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
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
        public int StoreCount;
        public int RemoveQueueLength, RemoveQueueMax;
        public int AddQueueLength, AddQueueMax;
        public int BatchMs, BatchMsMax;
        public int TickMs, JobMs;
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

        private const int GrowDecayChance = 25;
        private const int BusyThreshold = 200;
        private const int BusyDeadChance = 10;
        private const int JobBatchSize = 64;

        private readonly ObjectSystem objectSystem;
        private readonly ObjectInfoRegistry registry;
        private readonly MapGenerator generator;
        private readonly CancellationToken token;

        private readonly NalRules rules;
        private readonly NalStore store;
        private NativeArray<NalDecision> decisions;
        private NativeArray<int> counter;
        private NativeList<int> dropSlots;
        private readonly Queue<RemoveOp> removeQueue = new(64);
        private readonly Queue<Pair<Vector2Int, ObjectInfo>> addQueue = new(64);

        private readonly int applyIntervalMs;
        private readonly long reportIntervalTicks = 10 * Stopwatch.Frequency;
        private long lastReportTicks;
        public readonly NalStats Stats = new();
        private double lastTickTime = -1d;
        private bool disposed;

        public NAL_PC(MapGenerator generator, ObjectSystem objectSystem, CancellationToken token, int applyIntervalMs, int storeCapacity)
        {
            this.generator = generator;
            this.objectSystem = objectSystem;
            this.token = token;
            this.applyIntervalMs = applyIntervalMs;

            registry = objectSystem.infoRegistry;
            rules = new NalRules(registry);
            store = new NalStore(storeCapacity);
            decisions = new NativeArray<NalDecision>(math.max(generator.NalMaxDecisionsPerTick, 1), Allocator.Persistent);
            counter = new NativeArray<int>(1, Allocator.Persistent);
            dropSlots = new NativeList<int>(64, Allocator.Persistent);
        }

        public void Dispose()
        {
            disposed = true;
            rules.Dispose();
            store.Dispose();
            if (decisions.IsCreated) decisions.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (dropSlots.IsCreated) dropSlots.Dispose();
        }

        public void Enqueue(Vector2Int pos, int prefabId)
        {
            if (disposed) return;

            long packed = Position2Int.Pack(pos);
            if (!generator.IsInActiveWindow(packed)) return;

            int dense = registry.DenseIndexOf(prefabId);
            if (dense < 0 || rules.Species[dense].Kind == 0) return;

            if (store.Add(pos, prefabId, dense, SimulationClock.TimeD) && store.Count > Stats.QueueMax)
                Stats.QueueMax = store.Count;
        }

        public void Clear()
        {
            if (!disposed) store.Clear();
        }

        public async UniTaskVoid RunNAL()
        {
            await UniTask.Delay(10000);

            while (!token.IsCancellationRequested && !disposed)
            {
                if (store.Count == 0)
                {
                    await UniTask.Yield();
                    continue;
                }

                double now = SimulationClock.TimeD;
                if (now == lastTickTime && Stats.DueBacklog == 0)
                {
                    await UniTask.Yield();
                    continue;
                }

                lastTickTime = now;
                long start = Stopwatch.GetTimestamp();

                int capacity = decisions.Length;
                counter[0] = 0;

                var job = new NalDecideJob
                {
                    Cell = store.Cell.AsArray(),
                    Dense = store.Dense.AsArray(),
                    Due = store.Due.AsArray(),
                    Species = rules.Species,
                    Output = decisions,
                    Counter = counter,
                    Now = now,
                    DeadChance = store.Count > BusyThreshold ? BusyDeadChance : 0,
                    Seed = (uint)RavineRandom.RangeInt(1, int.MaxValue),
                    SecondsPerDelayUnit = generator.NalSecondsPerDelayUnit,
                    FallbackDelaySeconds = applyIntervalMs * 0.00001f,
                    GrowDecayChance = GrowDecayChance
                };

                job.ScheduleParallel(store.Count, JobBatchSize, default).Complete();

                long jobEnd = Stopwatch.GetTimestamp();
                Stats.JobMs = (int)((jobEnd - start) * 1000 / Stopwatch.Frequency);

                int produced = counter[0];
                int written = math.min(produced, capacity);
                Stats.DueBacklog = produced - written;
                Stats.Processed += written;

                Apply(written);
                DropSlots();

                Stats.StoreCount = store.Count;
                Stats.TickMs = (int)((Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency);

                await UniTask.Yield();
            }
        }

        private void Apply(int count)
        {
            for (int i = 0; i < count; i++)
            {
                NalDecision d = decisions[i];
                int slot = d.Slot;
                int2 cell = store.Cell[slot];
                var pos = new Vector2Int(cell.x, cell.y);
                long packed = Position2Int.Pack(pos);
                int prefabId = store.PrefabId[slot];

                if (!generator.TryGetObject(packed, out ObjectInstInfo inst) || inst.PrefabID != prefabId)
                {
                    Stats.StaleRemovals++;
                    dropSlots.Add(slot);
                    continue;
                }

                NalSpeciesParams p = rules.Species[store.Dense[slot]];

                if (p.Kind == (byte)BehaviourType.GROW)
                {
                    if (p.EvolutionDense < 0)
                    {
                        if (d.DeathRoll)
                        {
                            removeQueue.Enqueue(new RemoveOp(pos, prefabId));
                            store.Due[slot] = double.MaxValue;
                        }
                        else
                        {
                            Reschedule(slot, packed, d.NextDue);
                        }
                        continue;
                    }

                    removeQueue.Enqueue(new RemoveOp(pos, prefabId));
                    addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(pos, registry.GetDense(p.EvolutionDense)));
                    store.Due[slot] = double.MaxValue;
                    continue;
                }

                if (p.Kind != (byte)BehaviourType.NAL)
                {
                    dropSlots.Add(slot);
                    continue;
                }

                if (d.DeathRoll || HasNeighbour(pos))
                {
                    removeQueue.Enqueue(new RemoveOp(pos, prefabId));
                    EnqueueDeathPattern(pos, p);
                    store.Due[slot] = double.MaxValue;
                    continue;
                }

                if (!generator.IsHeightIsLiveAble(packed))
                {
                    dropSlots.Add(slot);
                    continue;
                }

                if (d.SpawnCount > 0)
                {
                    ObjectInfo spawn = registry.GetDense(d.SpawnDense);
                    for (int k = 0; k < d.SpawnCount; k++)
                        addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(new Vector2Int(d.TargetsX[k], d.TargetsZ[k]), spawn));
                }

                Reschedule(slot, packed, d.NextDue);
            }
        }

        private bool HasNeighbour(Vector2Int pos)
        {
            for (int x = -MapGenerator.scale; x <= MapGenerator.scale; x++)
                for (int y = -MapGenerator.scale; y <= MapGenerator.scale; y++)
                    if ((x != 0 || y != 0) && generator.ContainsObject(Position2Int.Pack(pos + new Vector2Int(x, y))))
                        return true;

            return false;
        }

        private void EnqueueDeathPattern(Vector2Int pos, in NalSpeciesParams p)
        {
            if (p.DeathMainDense >= 0)
                addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(pos, registry.GetDense(p.DeathMainDense)));

            for (int k = 0; k < p.DeathOtherCount; k++)
            {
                ObjectInfo other = registry.GetDense(rules.OtherDense[p.DeathOtherStart + k]);
                Vector2Int point = Extension.GetRandomPointAround(pos, p.DeathFactor);
                addQueue.Enqueue(new Pair<Vector2Int, ObjectInfo>(point, other));
            }
        }

        private void Reschedule(int slot, long packed, double nextDue)
        {
            if (generator.IsInActiveWindow(packed))
                store.Due[slot] = nextDue;
            else
                dropSlots.Add(slot);
        }

        private void DropSlots()
        {
            if (dropSlots.Length == 0) return;

            dropSlots.Sort();
            for (int i = dropSlots.Length - 1; i >= 0; i--)
            {
                int2 cell = store.Cell[dropSlots[i]];
                store.Remove(Position2Int.Pack(cell.x, cell.y));
            }

            dropSlots.Clear();
        }

        private void RemoveFromStore(long pos, int prefabId)
        {
            if (store.TryGetSlot(pos, out int slot) && store.PrefabId[slot] == prefabId)
                store.Remove(pos);
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

                    RemoveFromStore(pos, op.PrefabId);

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
                        if (item.Second.HasLifecycle) Enqueue(item.First, item.Second.Id);
                    }
                    else
                    {
                        Stats.BornRejected++;
                    }
                }

                generator.ExtraUpdate();

                Debug.Assert(removeQueue.Count == 0 && addQueue.Count == 0, "NAL: apply queues not drained");
                Debug.Assert(store.IsConsistent(), "NAL: store desync");

                long end = Stopwatch.GetTimestamp();
                Stats.BatchMs = (int)((end - start) * 1000 / Stopwatch.Frequency);
                if (Stats.BatchMs > Stats.BatchMsMax) Stats.BatchMsMax = Stats.BatchMs;
                Stats.QueueLength = store.Count;

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
                $"queue={s.QueueLength}/{s.QueueMax} store={s.StoreCount} removeQ={s.RemoveQueueLength}/{s.RemoveQueueMax} " +
                $"addQ={s.AddQueueLength}/{s.AddQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax} tickMs={s.TickMs} jobMs={s.JobMs}");
        }
    }
}
