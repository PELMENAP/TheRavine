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
        public int Ticks, Events, EventsDropped, CatchUpRounds;
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

        private NativeList<RegionChunk> region;
        private NativeArray<ChunkTickSource> sources;
        private NativeArray<RandomTickEvent> events;
        private NativeArray<int> counter;
        private NativeHashMap<long, int> members;
        private NativeHashMap<long, double> leftAt;
        private NativeHashMap<long, int> owed;
        private NativeList<long> stale;

        public readonly RandomTickStats Stats = new();
        public WorldTickRules Rules => rules;

        private double lastTime = -1d;
        private double warmupUntil;
        private double accumulator;
        private long regionCenter;
        private int regionFacing;
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

            region = new NativeList<RegionChunk>(64, Allocator.Persistent);
            sources = new NativeArray<ChunkTickSource>(64, Allocator.Persistent);
            events = new NativeArray<RandomTickEvent>(math.max(rules.maxEventsPerTick, 1), Allocator.Persistent);
            counter = new NativeArray<int>(1, Allocator.Persistent);
            members = new NativeHashMap<long, int>(64, Allocator.Persistent);
            leftAt = new NativeHashMap<long, double>(64, Allocator.Persistent);
            owed = new NativeHashMap<long, int>(16, Allocator.Persistent);
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
                || generator.Facing != regionFacing
                || generator.NalRingCount != regionRings)
                RebuildRegion(now);

            long start = Stopwatch.GetTimestamp();
            int n = FillSources();
            Stats.RegionChunks = n;
            counter[0] = 0;

            if (n > 0)
            {
                new RandomTickJob
                {
                    Sources = sources,
                    AcceptById = acceptById,
                    Output = events,
                    Counter = counter,
                    CellsPerChunkPerTick = rules.cellsPerChunkPerTick,
                    FarCellDivisor = math.max(rules.farCellDivisor, 1f),
                    TotalCells = ChunkData.TotalCells,
                    Seed = (uint)RavineRandom.RangeInt(1, int.MaxValue)
                }.ScheduleParallel(n, JobBatchSize, default).Complete();
            }

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

        private int FillSources()
        {
            int n = 0;
            bool anyOwed = owed.Count > 0;

            for (int i = 0; i < region.Length; i++)
            {
                RegionChunk r = region[i];
                if (!generator.TryGetChunk(r.Key, out ChunkData cd) || cd.DetailLevel > r.Ring
                    || !cd.Occupancy.IsCreated || !cd.Objects.IsCreated) continue;

                int rounds = 1;
                if (anyOwed && owed.TryGetValue(r.Key, out int debt))
                {
                    int extra = math.min(debt, rules.catchUpBurstRounds);
                    rounds += extra;
                    Stats.CatchUpRounds += extra;
                    if (extra > 0 && debt > extra) owed[r.Key] = debt - extra;
                    else owed.Remove(r.Key);
                }

                sources[n++] = ChunkTickSource.Create(cd.Occupancy, cd.Objects, r.Key, r.Ring, rounds);
            }

            return n;
        }

        private void RebuildRegion(double now)
        {
            regionBuilt = true;
            framesSinceRebuild = 0;
            regionCenter = generator.WindowCenter;
            regionFacing = generator.Facing;
            regionRings = generator.NalRingCount;
            stamp++;
            region.Clear();

            for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
                for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
                    AddRegionChunk(MapGenerator.WindowChunk(regionCenter, gx, gz), 0, now);

            for (int ring = 1; ring <= regionRings; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    long block = Position2Int.Offset(regionCenter, dx * MapGenerator.WindowSide, ring * regionFacing * MapGenerator.WindowSide);
                    for (int gz = 0; gz < MapGenerator.WindowSide; gz++)
                        for (int gx = 0; gx < MapGenerator.WindowSide; gx++)
                            AddRegionChunk(MapGenerator.WindowChunk(block, gx, gz), ring, now);
                }
            }

            stale.Clear();
            foreach (var kv in members)
                if (kv.Value != stamp) stale.Add(kv.Key);

            for (int i = 0; i < stale.Length; i++)
                Leave(stale[i], now);

            if (region.Length > sources.Length)
            {
                sources.Dispose();
                sources = new NativeArray<ChunkTickSource>(region.Length, Allocator.Persistent);
            }
        }

        private void AddRegionChunk(long key, int ring, double now)
        {
            region.Add(new RegionChunk { Key = key, Ring = ring });

            if (!generator.TryGetChunk(key, out ChunkData cd) || cd.DetailLevel > ring) return;

            if (!members.ContainsKey(key)) Enter(key, now);
            members[key] = stamp;
        }

        private void Enter(long key, double now)
        {
            if (!leftAt.TryGetValue(key, out double left)) return;

            leftAt.Remove(key);
            if (rules.catchUpBurstRounds <= 0) return;

            double elapsed = math.min(math.max(now - left, 0d), rules.catchUpMaxSeconds);
            int debt = (int)math.floor(elapsed / rules.tickSeconds);
            if (debt > 0) owed[key] = debt;
        }

        private void Leave(long key, double now)
        {
            members.Remove(key);
            owed.Remove(key);
            leftAt[key] = now;
        }

        public void OnChunkReleased(long key)
        {
            if (disposed || !members.ContainsKey(key)) return;
            Leave(key, SimulationClock.TimeD);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (acceptById.IsCreated) acceptById.Dispose();
            if (region.IsCreated) region.Dispose();
            if (sources.IsCreated) sources.Dispose();
            if (events.IsCreated) events.Dispose();
            if (counter.IsCreated) counter.Dispose();
            if (members.IsCreated) members.Dispose();
            if (leftAt.IsCreated) leftAt.Dispose();
            if (owed.IsCreated) owed.Dispose();
            if (stale.IsCreated) stale.Dispose();
        }
    }
}
