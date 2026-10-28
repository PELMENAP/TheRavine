using System.Diagnostics;
using UnityEngine;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class NalStats
    {
        public int Processed;
        public int BornApplied, BornRejected;
        public int Deaths, RemoveNoEffect, StaleRemovals;
        public int CommandQueueLength, CommandQueueMax;
        public int BatchMs, BatchMsMax;
        public int RollDeaths, CloseDeaths, GrowDecays, Evolved;
        public int StaleDrops, HeightDrops, WindowDrops;
        public int SpawnPushed;
        public int FarEvents, Decays, RegionChunks;
        public int Ticks, Events, EventsDropped, CatchUpRounds;
        public float TicksPerSecond, JobMs, DispatchMs;
        public double ClockTime;
    }

    public sealed class NalTickHandler : IRandomTickHandler
    {
        private const int GrowDecayChance = 25;
        private const int MaxSpawns = 4;
        private const double ReportIntervalSeconds = 10d;
        private const int NeighbourRadiusCells = (MapGenerator.scale + (1 << MapGenerator.CellShift) - 1) >> MapGenerator.CellShift;

        private readonly MapGenerator generator;
        private readonly ObjectInfoRegistry registry;
        private readonly RandomTickService ticks;
        private readonly WorldTickRules tickRules;
        private readonly WorldCommandBuffer commands;
        private readonly NalRules rules;

        private readonly long frozenCheckInterval = (long)(ReportIntervalSeconds * Stopwatch.Frequency);
        private long lastFrozenCheck = Stopwatch.GetTimestamp();
        private double frozenCheckClock = -1d;
        private double lastReportClock = -1d;
        private int lastReportTicks;
        public readonly NalStats Stats = new();
        private bool disposed;

        public NalTickHandler(MapGenerator generator, ObjectInfoRegistry registry, RandomTickService ticks)
        {
            this.generator = generator;
            this.registry = registry;
            this.ticks = ticks;
            tickRules = ticks.Rules;
            commands = generator.Commands;
            rules = new NalRules(registry);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            rules.Dispose();
        }

        public bool Accepts(int prefabId)
        {
            int dense = registry.DenseIndexOf(prefabId);
            return dense >= 0 && rules.Species[dense].Kind != 0;
        }

        public void OnRandomTick(in RandomTickEvent e, double now)
        {
            if (disposed) return;

            int dense = registry.DenseIndexOf(e.PrefabId);
            if (dense < 0) return;

            NalSpeciesParams p = rules.Species[dense];
            if (p.Kind == (byte)BehaviourType.NAL)
            {
                if (RavineRandom.RangeFloat() >= RandomTickMath.NalAcceptChance(p.Delay, tickRules.nalDelayReference)) return;
            }
            else if (p.Kind == (byte)BehaviourType.GROW)
            {
                if (RavineRandom.RangeFloat() >= tickRules.growAcceptChance) return;
            }
            else if (p.Kind != NalSpeciesParams.KindDecay) return;

            Stats.Processed++;
            if (e.Ring > 0) Stats.FarEvents++;

            int worldX = ((Position2Int.GetX(e.Chunk) << MapGenerator.RowShift) + (e.Cell & MapGenerator.RowMask)) << MapGenerator.CellShift;
            int worldZ = ((Position2Int.GetY(e.Chunk) << MapGenerator.RowShift) + (e.Cell >> MapGenerator.RowShift)) << MapGenerator.CellShift;
            long packed = Position2Int.Pack(worldX, worldZ);

            if (commands.HasPendingRemove(packed)
                || !generator.TryGetObject(packed, out ObjectInstInfo inst)
                || inst.PrefabID != e.PrefabId)
            {
                Stats.StaleDrops++;
                return;
            }

            int ring = generator.RingOfChunk(e.Chunk);
            if (ring < 0)
            {
                Stats.WindowDrops++;
                return;
            }

            if (p.Kind == (byte)BehaviourType.GROW)
            {
                if (p.EvolutionDense < 0)
                {
                    if (RavineRandom.RangeInt(1, 101) <= GrowDecayChance)
                    {
                        Stats.GrowDecays++;
                        PushRemove(packed, e.PrefabId);
                    }
                    return;
                }

                Stats.Evolved++;
                PushRemove(packed, e.PrefabId);
                PushAdd(packed, registry.GetDense(p.EvolutionDense), ring);
                return;
            }

            if (p.Kind == NalSpeciesParams.KindDecay)
            {
                float meanHit = RandomTickMath.MeanHitSeconds(
                    ChunkData.TotalCells, tickRules.cellsPerChunkPerTick, tickRules.tickSeconds, e.Ring, tickRules.farCellDivisor);
                if (RavineRandom.RangeFloat() < RandomTickMath.DecayChance(meanHit, p.DecaySeconds))
                {
                    Stats.Decays++;
                    PushRemove(packed, e.PrefabId);
                }
                return;
            }

            var pos = new Vector2Int(worldX, worldZ);
            bool rolled = RavineRandom.RangeInt(1, 101) <= p.Chance >> 1;
            if (rolled || HasNeighbour(pos))
            {
                if (rolled) Stats.RollDeaths++;
                else Stats.CloseDeaths++;
                PushRemove(packed, e.PrefabId);
                PushDeathPattern(pos, p, ring);
                return;
            }

            if (!generator.IsHeightIsLiveAble(packed))
            {
                Stats.HeightDrops++;
                return;
            }

            if (p.EvolutionDense < 0) return;

            ObjectInfo spawn = registry.GetDense(p.EvolutionDense);
            int spawned = 0;
            for (int a = 0; a < p.Attempt && spawned < MaxSpawns; a++)
            {
                if (RavineRandom.RangeInt(1, 101) > p.Chance) continue;

                int tx = worldX + (int)(RavineRandom.RangeFloat(-1f, 1f) * p.Distance);
                int tz = worldZ + (int)(RavineRandom.RangeFloat(-1f, 1f) * p.Distance);
                spawned++;
                if (PushAdd(Position2Int.Pack(tx, tz), spawn, ring)) Stats.SpawnPushed++;
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

        private bool PushAdd(long pos, ObjectInfo info, int ring)
        {
            if (info == null) return false;
            if (ring > 0 && !generator.HasChunk(MapGenerator.WorldToChunk(pos))) return false;

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

        private void PushDeathPattern(Vector2Int pos, in NalSpeciesParams p, int ring)
        {
            if (p.DeathMainDense >= 0)
                PushAdd(Position2Int.Pack(pos), registry.GetDense(p.DeathMainDense), ring);

            for (int k = 0; k < p.DeathOtherCount; k++)
            {
                ObjectInfo other = registry.GetDense(rules.OtherDense[p.DeathOtherStart + k]);
                Vector2Int point = Extension.GetRandomPointAround(pos, p.DeathFactor);
                PushAdd(Position2Int.Pack(point), other, ring);
            }
        }

        public void OnCommandsApplied(WorldCommandStats applied, int applyMs, int pendingBefore, int pendingAfter)
        {
            if (disposed) return;

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
        }

        public void Poll()
        {
            if (disposed) return;

            double clock = SimulationClock.TimeD;
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp - lastFrozenCheck >= frozenCheckInterval)
            {
                lastFrozenCheck = timestamp;
                if (clock == frozenCheckClock && WorldAuthority.IsAuthority)
                    RavineLog.Warning($"[NAL] SimulationClock is frozen at {clock:F2} s: nothing advances world time, so no random tick runs");
                frozenCheckClock = clock;
            }

            RandomTickStats t = ticks.Stats;
            if (lastReportClock < 0d || clock < lastReportClock)
            {
                lastReportClock = clock;
                lastReportTicks = t.Ticks;
                return;
            }

            double elapsed = clock - lastReportClock;
            if (elapsed < ReportIntervalSeconds) return;

            NalStats s = Stats;
            s.TicksPerSecond = (float)((t.Ticks - lastReportTicks) / elapsed);
            s.Ticks = t.Ticks;
            s.Events = t.Events;
            s.EventsDropped = t.EventsDropped;
            s.CatchUpRounds = t.CatchUpRounds;
            s.RegionChunks = t.RegionChunks;
            s.JobMs = t.JobMs;
            s.DispatchMs = t.DispatchMs;
            s.ClockTime = clock;

            lastReportClock = clock;
            lastReportTicks = t.Ticks;
            Report();
        }

        private void Report()
        {
            NalStats s = Stats;
            RavineLog.Info(
                $"[NAL] processed={s.Processed} far={s.FarEvents} born={s.BornApplied}/{s.BornRejected} " +
                $"deaths={s.Deaths} removeNoEffect={s.RemoveNoEffect} stale={s.StaleRemovals} " +
                $"deathRoll={s.RollDeaths} deathClose={s.CloseDeaths} growDecay={s.GrowDecays} evolved={s.Evolved} " +
                $"spawnPushed={s.SpawnPushed} decay={s.Decays} dropStale={s.StaleDrops} dropHeight={s.HeightDrops} dropWindow={s.WindowDrops} " +
                $"ticks={s.Ticks} tps={s.TicksPerSecond:F2} events={s.Events} eventsDropped={s.EventsDropped} catchUp={s.CatchUpRounds} " +
                $"regionChunks={s.RegionChunks} clock={s.ClockTime:F2} " +
                $"cmdQ={s.CommandQueueLength}/{s.CommandQueueMax} batchMs={s.BatchMs}/{s.BatchMsMax} jobMs={s.JobMs:F3} dispatchMs={s.DispatchMs:F3}");
        }
    }
}
