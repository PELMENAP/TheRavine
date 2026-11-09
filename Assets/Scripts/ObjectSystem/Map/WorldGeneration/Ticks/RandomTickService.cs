using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class RandomTickStats
    {
        public int Ticks, Events, EventsDropped;
        public int CatchUpEpochs, CatchUpBudgetHits, CatchUpTicksSimulated;
        public int RegionChunks;
        public float JobMs, DispatchMs;
    }

    public sealed class RandomTickService
    {
        private const int MaxHandlers = 32;
        private const int JobBatchSize = 8;

        private struct RegionChunk
        {
            public long Key;
            public int Ring;
        }

        private readonly MapGenerator generator;
        private readonly ObjectInfoRegistry registry;
        private readonly WorldTickRules rules;

        private IRandomTickHandler[] handlers = new IRandomTickHandler[4];
        private int handlerCount;
        private readonly uint[] handlerByDense;
        private NativeArray<byte> acceptById;
        private NativeArray<byte> spreadById;

        private NativeList<RegionChunk> region;
        private NativeArray<ChunkTickSource> sources;
        private NativeArray<ChunkTickSource> debtSources;
        private NativeArray<RandomTickEvent> events;
        private NativeArray<int> counter;
        private NativeHashMap<long, int> members;
        private NativeList<long> stale;

        public readonly RandomTickStats Stats = new();
        public WorldTickRules Rules => rules;
        public long WorldTick => generator.WorldTick;

        private double lastTime = -1d;
        private double warmupUntil;
        private double accumulator;
        private long regionCenter;
        private int regionRings;
        private int framesSinceRebuild;
        private int stamp;
        private bool regionBuilt;
        private bool disposed;

        public RandomTickService(MapGenerator generator, ObjectInfoRegistry registry, WorldTickRules rules)
        {
            this.generator = generator;
            this.registry = registry;
            this.rules = rules;

            handlerByDense = new uint[registry.Count];
            acceptById = new NativeArray<byte>(math.max(registry.IdCapacity, 1), Allocator.Persistent);
            spreadById = new NativeArray<byte>(math.max(registry.IdCapacity, 1), Allocator.Persistent);

            region = new NativeList<RegionChunk>(64, Allocator.Persistent);
            sources = new NativeArray<ChunkTickSource>(64, Allocator.Persistent);
            debtSources = new NativeArray<ChunkTickSource>(64, Allocator.Persistent);
            events = new NativeArray<RandomTickEvent>(math.max(rules.maxEventsPerTick, 1), Allocator.Persistent);
            counter = new NativeArray<int>(1, Allocator.Persistent);
            members = new NativeHashMap<long, int>(64, Allocator.Persistent);
            stale = new NativeList<long>(16, Allocator.Persistent);
        }

        public void Register(IRandomTickHandler handler)
        {
            if (disposed || handler == null) return;
            if (handlerCount == MaxHandlers)
            {
                RavineLog.Error($"[RandomTick] handler limit {MaxHandlers} reached, {handler.GetType().Name} ignored");
                return;
            }

            if (handlerCount == handlers.Length)
                Array.Resize(ref handlers, handlerCount * 2);

            int bit = handlerCount;
            handlers[handlerCount++] = handler;

            for (int d = 0; d < handlerByDense.Length; d++)
            {
                ObjectInfo info = registry.GetDense(d);
                if (info == null || !handler.Accepts(info.Id)) continue;

                handlerByDense[d] |= 1u << bit;
                if (info.Id < acceptById.Length) acceptById[info.Id] = 1;
                if (info.Id < spreadById.Length && handler.AcceptsSpread(info.Id)) spreadById[info.Id] = 1;
            }
        }

        public void Update()
        {
            if (disposed) return;

            framesSinceRebuild++;
            double now = SimulationClock.TimeD;

            if (lastTime < 0d || now < lastTime)
            {
                lastTime = now;
                warmupUntil = now + rules.warmupSeconds;
                accumulator = 0d;
                return;
            }

            if (!WorldAuthority.IsAuthority)
            {
                lastTime = now;
                accumulator = 0d;
                return;
            }

            accumulator += now - lastTime;
            lastTime = now;

            if (now < warmupUntil)
            {
                accumulator = 0d;
                return;
            }

            double tick = rules.tickSeconds;
            int max = rules.maxTicksPerFrame;
            for (int done = 0; accumulator >= tick && done < max; done++)
            {
                accumulator -= tick;
                Tick(now);
            }

            if (accumulator >= tick) accumulator %= tick;
        }

        private void Tick(double now)
        {
            if (!regionBuilt
                || framesSinceRebuild >= rules.regionRebuildIntervalFrames
                || generator.WindowCenter != regionCenter
                || generator.NalRingCount != regionRings)
                RebuildRegion();

            long start = Stopwatch.GetTimestamp();
            int n = FillSources(out int debtCount);
            Stats.RegionChunks = n;
            counter[0] = 0;

            if (n > 0) RunTickJob(sources, acceptById, n);
            if (debtCount > 0) RunTickJob(debtSources, spreadById, debtCount);

            long jobEnd = Stopwatch.GetTimestamp();
            Stats.JobMs = (float)((jobEnd - start) * 1000d / Stopwatch.Frequency);

            int produced = counter[0];
            int written = math.min(produced, events.Length);
            Stats.Ticks++;
            Stats.Events += written;
            Stats.EventsDropped += produced - written;

            Dispatch(written, now);
            Stats.DispatchMs = (float)((Stopwatch.GetTimestamp() - jobEnd) * 1000d / Stopwatch.Frequency);
        }

        private void RunTickJob(NativeArray<ChunkTickSource> source, NativeArray<byte> accept, int length)
        {
            new RandomTickJob
            {
                Sources = source,
                AcceptById = accept,
                Output = events,
                Counter = counter,
                CellsPerChunkPerTick = rules.cellsPerChunkPerTick,
                FarCellDivisor = math.max(rules.farCellDivisor, 1f),
                TotalCells = ChunkData.TotalCells,
                Seed = (uint)RavineRandom.RangeInt(1, int.MaxValue)
            }.ScheduleParallel(length, JobBatchSize, default).Complete();
        }

        private void Dispatch(int count, double now)
        {
            for (int i = 0; i < count; i++)
            {
                RandomTickEvent e = events[i];
                int dense = registry.DenseIndexOf(e.PrefabId);
                if (dense < 0) continue;

                uint mask = handlerByDense[dense];
                for (int h = 0; mask != 0u; h++, mask >>= 1)
                    if ((mask & 1u) != 0u) handlers[h].OnRandomTick(in e, now);
            }
        }

        private int FillSources(out int debtCount)
        {
            int n = 0;
            debtCount = 0;

            for (int i = 0; i < region.Length; i++)
            {
                RegionChunk r = region[i];
                if (!generator.TryGetChunk(r.Key, out ChunkData cd) || cd.DetailLevel > r.Ring
                    || !cd.Occupancy.IsCreated || !cd.Objects.IsCreated) continue;

                sources[n++] = ChunkTickSource.Create(cd.Occupancy, cd.Objects, r.Key, r.Ring, 1);

                if (cd.SpreadOwed <= 0) continue;

                int extra = math.min(cd.SpreadOwed, rules.catchUpBurstRounds);
                if (extra <= 0)
                {
                    cd.SpreadOwed = 0;
                    continue;
                }

                cd.SpreadOwed -= extra;
                Stats.CatchUpTicksSimulated += extra;
                debtSources[debtCount++] = ChunkTickSource.Create(cd.Occupancy, cd.Objects, r.Key, r.Ring, extra);
            }

            return n;
        }

        private void RebuildRegion()
        {
            regionBuilt = true;
            framesSinceRebuild = 0;
            regionCenter = generator.WindowCenter;
            regionRings = generator.NalRingCount;
            stamp++;
            region.Clear();

            int reach = regionRings * MapGenerator.WindowSide + MapGenerator.chunkScale;
            int cx = Position2Int.GetX(regionCenter);
            int cz = Position2Int.GetY(regionCenter);

            for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    long key = Position2Int.Pack(cx + dx, cz + dz);
                    AddRegionChunk(key, MapGenerator.SimRingOf(regionCenter, regionRings, key));
                }

            stale.Clear();
            foreach (var kv in members)
                if (kv.Value != stamp) stale.Add(kv.Key);

            for (int i = 0; i < stale.Length; i++)
                Leave(stale[i]);

            if (region.Length > sources.Length)
            {
                sources.Dispose();
                debtSources.Dispose();
                sources = new NativeArray<ChunkTickSource>(region.Length, Allocator.Persistent);
                debtSources = new NativeArray<ChunkTickSource>(region.Length, Allocator.Persistent);
            }
        }

        private void AddRegionChunk(long key, int ring)
        {
            region.Add(new RegionChunk { Key = key, Ring = ring });

            if (!generator.TryGetChunk(key, out ChunkData cd) || cd.DetailLevel > ring) return;

            if (!members.ContainsKey(key)) Enter(key, ring, cd);
            members[key] = stamp;
        }

        private void Enter(long key, int ring, ChunkData cd)
        {
            long now = WorldTick;
            long delta = now - cd.ObjectTick;
            cd.ObjectTick = now;
            if (delta <= 0L) return;

            for (int h = 0; h < handlerCount; h++)
                handlers[h].OnChunkEnter(key, cd, delta, ring);

            if (rules.catchUpBurstRounds <= 0) return;

            long cap = (long)math.floor(rules.catchUpMaxSeconds / rules.tickSeconds);
            cd.SpreadOwed = (int)Math.Min(delta, cap);
        }

        private void Leave(long key)
        {
            members.Remove(key);
            if (!generator.TryGetChunk(key, out ChunkData cd)) return;

            cd.ObjectTick = WorldTick;
            cd.SpreadOwed = 0;
        }

        public void StampActive(long key, ChunkData chunk)
        {
            if (!disposed && members.ContainsKey(key)) chunk.ObjectTick = WorldTick;
        }

        public void OnChunkReleased(long key)
        {
            if (disposed || !members.ContainsKey(key)) return;
            Leave(key);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (acceptById.IsCreated) acceptById.Dispose();
            if (spreadById.IsCreated) spreadById.Dispose();
            if (region.IsCreated) region.Dispose();
            if (sources.IsCreated) sources.Dispose();
            if (debtSources.IsCreated) debtSources.Dispose();
            if (events.IsCreated) events.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (members.IsCreated) members.Dispose();
            if (stale.IsCreated) stale.Dispose();
        }
    }
}
