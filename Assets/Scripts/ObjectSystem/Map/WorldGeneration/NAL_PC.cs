using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

using TheRavine.Base;
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
        public int CommandQueueLength, CommandQueueMax;
        public int BatchMs, BatchMsMax;
        public int TickMs, JobMs;
        public int RollDeaths, CloseDeaths, GrowDecays, Evolved;
        public int StaleDrops, HeightDrops, WindowDrops;
        public int SpawnPushed;
    }

    public class NAL_PC
    {
        private const int GrowDecayChance = 25;
        private const int BusyThreshold = 200;
        private const int BusyDeadChance = 10;
        private const int JobBatchSize = 64;
        private const int NeighbourRadiusCells = (MapGenerator.scale + (1 << MapGenerator.CellShift) - 1) >> MapGenerator.CellShift;

        private readonly ObjectSystem objectSystem;
        private readonly ObjectInfoRegistry registry;
        private readonly MapGenerator generator;
        private readonly CancellationToken token;

        private readonly NalRules rules;
        private readonly NalStore store;
        private NativeArray<NalDecision> decisions;
        private NativeArray<int> counter;
        private NativeList<int> dropSlots;
        private readonly WorldCommandBuffer commands;

        private readonly long reportIntervalTicks = 10 * Stopwatch.Frequency;
        private long lastReportTicks = Stopwatch.GetTimestamp();
        public readonly NalStats Stats = new();
        private double lastTickTime = -1d;
        private double catchUpElapsed;
        private bool disposed;

        public NAL_PC(MapGenerator generator, ObjectSystem objectSystem, CancellationToken token, int storeCapacity)
        {
            this.generator = generator;
            this.objectSystem = objectSystem;
            this.token = token;
            commands = generator.Commands;
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

        public void Enqueue(Vector2Int pos, int prefabId) => Register(pos, prefabId, false);

        private void Register(Vector2Int pos, int prefabId, bool newborn)
        {
            if (disposed || !WorldAuthority.IsAuthority) return;

            long packed = Position2Int.Pack(pos);
            if (!generator.IsInActiveWindow(packed)) return;

            int dense = registry.DenseIndexOf(prefabId);
            if (dense < 0 || rules.Species[dense].Kind == 0) return;

            double now = SimulationClock.TimeD;
            double period = rules.Species[dense].Period(generator.NalSecondsPerDelayUnit, generator.NalGrowStageSeconds);
            double due = newborn ? now + period : now + RavineRandom.RangeFloat() * period;

            if (!newborn && catchUpElapsed > 0d)
            {
                double steps = period > 0d ? math.min((double)generator.NalCatchUpMaxSteps, math.floor(catchUpElapsed / period)) : 0d;
                if (steps >= 1d) due = now - (steps - 1d) * period;
            }

            if (store.Add(pos, prefabId, dense, due) && store.Count > Stats.QueueMax)
                Stats.QueueMax = store.Count;
        }

        public void BeginChunk(long chunk)
        {
            catchUpElapsed = 0d;
            if (disposed) return;

            if (store.TakeLeftAt(chunk, out double leftAt))
                catchUpElapsed = math.clamp(SimulationClock.TimeD - leftAt, 0d, generator.NalCatchUpMaxSeconds);
        }

        public void EndChunk() => catchUpElapsed = 0d;

        public void RemoveChunk(long chunk)
        {
            if (disposed) return;

            if (store.RemoveChunk(chunk) > 0)
                store.SetLeftAt(chunk, SimulationClock.TimeD);

            Stats.StoreCount = store.Count;
        }

        public void OnCommandsApplied(NativeList<WorldCommand> adds, WorldCommandStats applied, int applyMs, int pendingBefore, int pendingAfter)
        {
            if (disposed) return;

            for (int i = 0; i < adds.Length; i++)
                Register(Position2Int.UnpackToVector(adds[i].Pos), adds[i].PrefabId, true);

            NalStats s = Stats;
            s.BornApplied = applied.AddApplied;
            s.BornRejected = applied.AddRejected;
            s.Deaths = applied.RemoveApplied;
            s.RemoveNoEffect = applied.RemoveNoEffect;
            s.StaleRemovals = s.StaleDrops + applied.RemoveStale;
            s.CommandQueueLength = pendingAfter;
            if (pendingBefore > s.CommandQueueMax) s.CommandQueueMax = pendingBefore;
            s.BatchMs = applyMs;
            if (applyMs > s.BatchMsMax) s.BatchMsMax = applyMs;
            s.StoreCount = store.Count;
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
                TryReport();

                if (store.Count == 0 || !WorldAuthority.IsAuthority)
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
                    FallbackDelaySeconds = generator.NalGrowStageSeconds,
                    GrowDecayChance = GrowDecayChance,
                    CatchUpMaxSteps = generator.NalCatchUpMaxSteps
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
                Stats.QueueLength = store.Count;
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
                    Stats.StaleDrops++;
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
                            Stats.GrowDecays++;
                            PushRemove(packed, prefabId);
                            dropSlots.Add(slot);
                        }
                        else
                        {
                            Reschedule(slot, packed, d.NextDue);
                        }
                        continue;
                    }

                    Stats.Evolved++;
                    PushRemove(packed, prefabId);
                    PushAdd(packed, registry.GetDense(p.EvolutionDense));
                    dropSlots.Add(slot);
                    continue;
                }

                if (p.Kind != (byte)BehaviourType.NAL)
                {
                    dropSlots.Add(slot);
                    continue;
                }

                bool rolled = d.DeathRoll;
                bool crowded = !rolled && HasNeighbour(pos);
                if (rolled || crowded)
                {
                    if (rolled) Stats.RollDeaths++;
                    else Stats.CloseDeaths++;
                    PushRemove(packed, prefabId);
                    PushDeathPattern(pos, p);
                    dropSlots.Add(slot);
                    continue;
                }

                if (!generator.IsHeightIsLiveAble(packed))
                {
                    Stats.HeightDrops++;
                    dropSlots.Add(slot);
                    continue;
                }

                if (d.SpawnCount > 0)
                {
                    ObjectInfo spawn = registry.GetDense(d.SpawnDense);
                    for (int k = 0; k < d.SpawnCount; k++)
                        PushAdd(Position2Int.Pack(d.TargetsX[k], d.TargetsZ[k]), spawn);
                    Stats.SpawnPushed += d.SpawnCount;
                }

                Reschedule(slot, packed, d.NextDue);
            }
        }

        private bool HasNeighbour(Vector2Int pos)
        {
            int cellX = pos.x >> MapGenerator.CellShift;
            int cellZ = pos.y >> MapGenerator.CellShift;

            for (int dx = -NeighbourRadiusCells; dx <= NeighbourRadiusCells; dx++)
            {
                for (int dz = -NeighbourRadiusCells; dz <= NeighbourRadiusCells; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    long cell = Position2Int.Pack((cellX + dx) << MapGenerator.CellShift, (cellZ + dz) << MapGenerator.CellShift);
                    if (generator.ContainsObject(cell) && !commands.HasPendingRemove(cell)) return true;
                }
            }

            return false;
        }

        private void PushRemove(long pos, int prefabId) =>
            commands.Push(new WorldCommand
            {
                Kind = WorldCommand.Remove,
                Pos = pos,
                PrefabId = prefabId,
                ExpectedPrefabId = prefabId
            });

        private void PushAdd(long pos, ObjectInfo info)
        {
            if (info == null) return;

            commands.Push(new WorldCommand
            {
                Kind = WorldCommand.Add,
                Pos = pos,
                PrefabId = info.Id,
                Amount = info.DefaultAmount,
                ExpectedPrefabId = WorldCommand.NoExpectation,
                Levels = 1
            });
        }

        private void PushDeathPattern(Vector2Int pos, in NalSpeciesParams p)
        {
            if (p.DeathMainDense >= 0)
                PushAdd(Position2Int.Pack(pos), registry.GetDense(p.DeathMainDense));

            for (int k = 0; k < p.DeathOtherCount; k++)
            {
                ObjectInfo other = registry.GetDense(rules.OtherDense[p.DeathOtherStart + k]);
                Vector2Int point = Extension.GetRandomPointAround(pos, p.DeathFactor);
                PushAdd(Position2Int.Pack(point), other);
            }
        }

        private void Reschedule(int slot, long packed, double nextDue)
        {
            if (generator.IsInActiveWindow(packed))
                store.Due[slot] = nextDue;
            else
            {
                Stats.WindowDrops++;
                dropSlots.Add(slot);
            }
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

        private void TryReport()
        {
            long now = Stopwatch.GetTimestamp();
            if (now - lastReportTicks < reportIntervalTicks) return;

            lastReportTicks = now;
            Debug.Assert(store.IsConsistent(), "NAL: store desync");
            Report();
        }

        private void Report()
        {
            NalStats s = Stats;
            RavineLog.Info(
                $"[NAL] processed={s.Processed} dueBacklog={s.DueBacklog} born={s.BornApplied}/{s.BornRejected} " +
                $"deaths={s.Deaths} removeNoEffect={s.RemoveNoEffect} stale={s.StaleRemovals} " +
                $"queue={s.QueueLength}/{s.QueueMax} store={s.StoreCount} " +
                $"deathRoll={s.RollDeaths} deathClose={s.CloseDeaths} growDecay={s.GrowDecays} evolved={s.Evolved} " +
                $"spawnPushed={s.SpawnPushed} dropStale={s.StaleDrops} dropHeight={s.HeightDrops} dropWindow={s.WindowDrops} " +
                $"cmdQ={s.CommandQueueLength}/{s.CommandQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax} tickMs={s.TickMs} jobMs={s.JobMs}");
        }
    }
}
