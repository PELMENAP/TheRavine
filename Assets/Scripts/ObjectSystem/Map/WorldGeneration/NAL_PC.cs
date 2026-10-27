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
        public int FarDecisions, Decays, RegionChunks;
    }

    public class NAL_PC
    {
        private const int GrowDecayChance = 25;
        private const int BusyThreshold = 200;
        private const int BusyDeadChance = 10;
        private const int JobBatchSize = 64;
        private const int MaxChunkRegistrationsPerSync = 2;
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
        private NativeList<long> staleChunks;
        private readonly WorldCommandBuffer commands;

        private readonly long reportIntervalTicks = 10 * Stopwatch.Frequency;
        private long lastReportTicks = Stopwatch.GetTimestamp();
        public readonly NalStats Stats = new();
        private double lastTickTime = -1d;
        private double catchUpElapsed;
        private int syncStamp;
        private bool tiersDirty;
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
            counter = new NativeArray<int>(2, Allocator.Persistent);
            dropSlots = new NativeList<int>(64, Allocator.Persistent);
            staleChunks = new NativeList<long>(16, Allocator.Persistent);
        }

        public void Dispose()
        {
            disposed = true;
            rules.Dispose();
            store.Dispose();
            if (decisions.IsCreated) decisions.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (dropSlots.IsCreated) dropSlots.Dispose();
            if (staleChunks.IsCreated) staleChunks.Dispose();
        }

        private void Register(Vector2Int pos, int prefabId, bool newborn, int tier)
        {
            if (disposed || !WorldAuthority.IsAuthority) return;

            int dense = registry.DenseIndexOf(prefabId);
            if (dense < 0 || rules.Species[dense].Kind == 0) return;

            if (tier < 0)
            {
                tier = generator.RingOfChunk(MapGenerator.WorldToChunk(Position2Int.Pack(pos)));
                if (tier < 0) return;
            }

            double now = SimulationClock.TimeD;
            double period = rules.Species[dense].Period(generator.NalSecondsPerDelayUnit, generator.NalGrowStageSeconds, generator.NalDecayCheckSeconds);
            if (tier > 0) period *= generator.NalFarPeriodScale * tier;
            double due = newborn ? now + period : now + RavineRandom.RangeFloat() * period;

            if (!newborn && catchUpElapsed > 0d)
            {
                double steps = period > 0d ? math.min((double)generator.NalCatchUpMaxSteps, math.floor(catchUpElapsed / period)) : 0d;
                if (steps >= 1d) due = now - (steps - 1d) * period;
            }

            if (store.Add(pos, prefabId, dense, due, (byte)tier) && store.Count > Stats.QueueMax)
                Stats.QueueMax = store.Count;
        }

        private void RegisterChunk(long chunk, ChunkData cd, int ring)
        {
            catchUpElapsed = 0d;
            if (store.TakeLeftAt(chunk, out double leftAt))
                catchUpElapsed = math.clamp(SimulationClock.TimeD - leftAt, 0d, generator.NalCatchUpMaxSeconds);

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                Register(
                    new Vector2Int((int)math.floor(info.Position.x), (int)math.floor(info.Position.z)),
                    info.PrefabID,
                    false,
                    ring);
            }

            catchUpElapsed = 0d;
        }

        public bool SyncRegion()
        {
            if (disposed || !WorldAuthority.IsAuthority) return false;

            syncStamp++;
            int budget = MaxChunkRegistrationsPerSync;
            bool pending = false;

            long center = generator.WindowCenter;
            for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
                for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
                    SyncChunk(MapGenerator.WindowChunk(center, gx, gz), 0, ref budget, ref pending);

            int rings = generator.NalRingCount;
            int facing = generator.Facing;
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    long block = Position2Int.Offset(center, dx * MapGenerator.WindowSide, ring * facing * MapGenerator.WindowSide);
                    for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
                        for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
                            SyncChunk(MapGenerator.WindowChunk(block, gx, gz), ring, ref budget, ref pending);
                }
            }

            staleChunks.Clear();
            foreach (var kv in store.Registered)
                if (kv.Value.Stamp != syncStamp) staleChunks.Add(kv.Key);

            for (int i = 0; i < staleChunks.Length; i++)
                RemoveChunk(staleChunks[i]);

            if (tiersDirty) RefreshTiers();

            Stats.RegionChunks = store.Registered.Count;
            Stats.StoreCount = store.Count;
            return pending;
        }

        private void SyncChunk(long key, int ring, ref int budget, ref bool pending)
        {
            bool known = store.Registered.TryGetValue(key, out NalChunkState state);

            if (!generator.TryGetChunk(key, out ChunkData cd) || cd.DetailLevel > ring)
            {
                if (known)
                {
                    state.Stamp = syncStamp;
                    store.Registered[key] = state;
                }
                return;
            }

            if (known && state.Ring != ring) tiersDirty = true;

            if (known && state.Detail == cd.DetailLevel)
            {
                state.Ring = (byte)ring;
                state.Stamp = syncStamp;
                store.Registered[key] = state;
                return;
            }

            if (budget == 0)
            {
                pending = true;
                if (known)
                {
                    state.Stamp = syncStamp;
                    store.Registered[key] = state;
                }
                return;
            }

            budget--;
            RegisterChunk(key, cd, ring);
            store.Registered[key] = new NalChunkState { Detail = cd.DetailLevel, Ring = (byte)ring, Stamp = syncStamp };
        }

        private void RefreshTiers()
        {
            tiersDirty = false;
            for (int i = 0; i < store.Count; i++)
            {
                long chunk = store.Chunk[i];
                store.Tier[i] = store.Registered.TryGetValue(chunk, out NalChunkState state)
                    ? state.Ring
                    : (byte)math.max(generator.RingOfChunk(chunk), 0);
            }
        }

        public void RemoveChunk(long chunk)
        {
            if (disposed) return;

            store.Registered.Remove(chunk);
            if (store.RemoveChunk(chunk) > 0)
                store.SetLeftAt(chunk, SimulationClock.TimeD);

            Stats.StoreCount = store.Count;
        }

        public void OnCommandsApplied(NativeList<WorldCommand> adds, WorldCommandStats applied, int applyMs, int pendingBefore, int pendingAfter)
        {
            if (disposed) return;

            for (int i = 0; i < adds.Length; i++)
                Register(Position2Int.UnpackToVector(adds[i].Pos), adds[i].PrefabId, true, -1);

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
                int nearCapacity = generator.NalFarBudgetShare > 0f
                    ? math.max(1, capacity - (int)(capacity * generator.NalFarBudgetShare))
                    : capacity;
                int farCapacity = capacity - nearCapacity;
                counter[0] = 0;
                counter[1] = 0;

                var job = new NalDecideJob
                {
                    Cell = store.Cell.AsArray(),
                    Dense = store.Dense.AsArray(),
                    Due = store.Due.AsArray(),
                    Tier = store.Tier.AsArray(),
                    Species = rules.Species,
                    Output = decisions,
                    Counter = counter,
                    Now = now,
                    DeadChance = store.Count > BusyThreshold ? BusyDeadChance : 0,
                    Seed = (uint)RavineRandom.RangeInt(1, int.MaxValue),
                    SecondsPerDelayUnit = generator.NalSecondsPerDelayUnit,
                    FallbackDelaySeconds = generator.NalGrowStageSeconds,
                    GrowDecayChance = GrowDecayChance,
                    CatchUpMaxSteps = generator.NalCatchUpMaxSteps,
                    NearCapacity = nearCapacity,
                    FarPeriodScale = generator.NalFarPeriodScale,
                    DecayCheckSeconds = generator.NalDecayCheckSeconds
                };

                job.ScheduleParallel(store.Count, JobBatchSize, default).Complete();

                long jobEnd = Stopwatch.GetTimestamp();
                Stats.JobMs = (int)((jobEnd - start) * 1000 / Stopwatch.Frequency);

                int nearProduced = counter[0];
                int farProduced = counter[1];
                int nearWritten = math.min(nearProduced, nearCapacity);
                int farWritten = math.min(farProduced, farCapacity);
                Stats.DueBacklog = (nearProduced - nearWritten) + (farProduced - farWritten);
                Stats.Processed += nearWritten + farWritten;
                Stats.FarDecisions += farWritten;

                Apply(0, nearWritten);
                Apply(nearCapacity, nearCapacity + farWritten);
                DropSlots();

                Stats.StoreCount = store.Count;
                Stats.QueueLength = store.Count;
                Stats.TickMs = (int)((Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency);

                await UniTask.Yield();
            }
        }

        private void Apply(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                NalDecision d = decisions[i];
                int slot = d.Slot;
                int2 cell = store.Cell[slot];
                var pos = new Vector2Int(cell.x, cell.y);
                long packed = Position2Int.Pack(pos);
                int prefabId = store.PrefabId[slot];
                int tier = store.Tier[slot];

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
                    PushAdd(packed, registry.GetDense(p.EvolutionDense), tier);
                    dropSlots.Add(slot);
                    continue;
                }

                if (p.Kind == NalSpeciesParams.KindDecay)
                {
                    if (d.DeathRoll)
                    {
                        Stats.Decays++;
                        PushRemove(packed, prefabId);
                        dropSlots.Add(slot);
                    }
                    else
                    {
                        Reschedule(slot, packed, d.NextDue);
                    }
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
                    PushDeathPattern(pos, p, tier);
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
                        if (PushAdd(Position2Int.Pack(d.TargetsX[k], d.TargetsZ[k]), spawn, tier)) Stats.SpawnPushed++;
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

        private bool PushAdd(long pos, ObjectInfo info, int tier)
        {
            if (info == null) return false;
            if (tier > 0 && !generator.HasChunk(MapGenerator.WorldToChunk(pos))) return false;

            commands.Push(new WorldCommand
            {
                Kind = WorldCommand.Add,
                Pos = pos,
                PrefabId = info.Id,
                Amount = info.DefaultAmount,
                ExpectedPrefabId = WorldCommand.NoExpectation,
                Levels = 1
            });
            return true;
        }

        private void PushDeathPattern(Vector2Int pos, in NalSpeciesParams p, int tier)
        {
            if (p.DeathMainDense >= 0)
                PushAdd(Position2Int.Pack(pos), registry.GetDense(p.DeathMainDense), tier);

            for (int k = 0; k < p.DeathOtherCount; k++)
            {
                ObjectInfo other = registry.GetDense(rules.OtherDense[p.DeathOtherStart + k]);
                Vector2Int point = Extension.GetRandomPointAround(pos, p.DeathFactor);
                PushAdd(Position2Int.Pack(point), other, tier);
            }
        }

        private void Reschedule(int slot, long packed, double nextDue)
        {
            if (generator.RingOfChunk(MapGenerator.WorldToChunk(packed)) >= 0)
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
                $"far={s.FarDecisions} decay={s.Decays} regionChunks={s.RegionChunks} " +
                $"cmdQ={s.CommandQueueLength}/{s.CommandQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax} tickMs={s.TickMs} jobMs={s.JobMs}");
        }
    }
}
