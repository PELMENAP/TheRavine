using UnityEngine;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;

public enum FoodKind : byte { Plant = 0, Toxic = 1, Large = 2, Meat = 3 }

public interface IFoodReceiver
{
    void ReceiveFood(float energy);
}

public struct ViralPayload
{
    public ushort[] Codons;
    public int      Count;
    public ulong    StrainId;
    public ulong    LineageId;
}

public struct FoodClaim
{
    public float    Energy;
    public FoodKind Kind;
    public bool     Pending;
    public bool     Infected;
}

public sealed class ChunkFoodIndex
{
    public const int FoodPrefabId = 0x0F00D;

    private const int   Size       = MapGenerator.mapChunkSize;
    private const int   ChunkShift = 6;
    private const int   ChunkMask  = Size - 1;
    private const float InvScale   = 1f / MapGenerator.scale;
    private const float HalfCell   = MapGenerator.scale * 0.5f;
    private const float WaterLevel = 5f;
    private readonly MapGenerator _map;
    private readonly LongDictionary<FoodChunk> _chunks = new(64);
    private readonly LongDictionary<float> _meat = new(32);
    private readonly LongDictionary<ViralPayload> _payloads = new(16);

    public int FoodCount { get; private set; }
    public int Revision  { get; private set; }

    public ChunkFoodIndex(MapGenerator map) => _map = map;

    private sealed class FoodChunk
    {
        public readonly ulong[] Rows     = new ulong[Size];
        public readonly ulong[] Infected = new ulong[Size];
        public readonly byte[]  Maturity = new byte[Size * Size];
        public int   Count;
        public int   BuiltVersion = -1;
        public ChunkData Source;
        public float Fertility = -1f;
        public float GrowthBudget;
        public float MaturityBudget;
    }

    private struct LargeClaim
    {
        public long          Cell;
        public double        Time;
        public IFoodReceiver Eater;
    }

    private LargeClaim[] _claims = new LargeClaim[8];
    private int _claimCount;

    private long[] _pruneKeys = new long[16];

    public static float2 CellCenter(long cell)
        => new float2(Position2Int.GetX(cell) * MapGenerator.scale + HalfCell,
                      Position2Int.GetY(cell) * MapGenerator.scale + HalfCell);

    public static long CellOf(float wx, float wz)
        => Position2Int.Pack(Mathf.FloorToInt(wx * InvScale), Mathf.FloorToInt(wz * InvScale));

    private FoodChunk Resolve(long chunkKey, out ChunkData cd)
    {
        if (!_map.TryGetChunk(chunkKey, out cd) || cd == null)
        {
            if (_chunks.TryRemove(chunkKey, out FoodChunk stale) && stale.Count != 0)
            {
                FoodCount -= stale.Count;
                Revision++;
            }
            cd = null;
            return null;
        }

        if (!_chunks.TryGetValue(chunkKey, out FoodChunk fc))
        {
            fc = new FoodChunk();
            _chunks[chunkKey] = fc;
        }

        if (fc.BuiltVersion != cd.Version || !ReferenceEquals(fc.Source, cd))
        {
            int oldCount = fc.Count;

            System.Array.Clear(fc.Rows, 0, Size);
            fc.Count = 0;

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                if (info.PrefabID != FoodPrefabId) continue;

                int idx = info.PrimaryIdx;
                if ((uint)idx >= (uint)ChunkData.TotalCells) continue;

                int lz = idx / Size;
                int lx = idx - lz * Size;
                fc.Rows[lz] |= 1UL << lx;
                fc.Count++;
            }

            for (int z = 0; z < Size; z++) fc.Infected[z] &= fc.Rows[z];

            if (!ReferenceEquals(fc.Source, cd)) fc.Fertility = -1f;
            fc.BuiltVersion = cd.Version;
            fc.Source       = cd;

            if (fc.Count != oldCount)
            {
                FoodCount += fc.Count - oldCount;
                Revision++;
            }
        }

        return fc;
    }

    public void PruneUnloaded()
    {
        if (_map == null || _chunks.Count == 0) return;

        EnsureKeyBuffer(_chunks.Count);

        int n = 0;
        foreach (var kv in _chunks)
        {
            if (!_map.TryGetChunk(kv.Key, out ChunkData cd) || cd == null || !ReferenceEquals(kv.Value.Source, cd))
                _pruneKeys[n++] = kv.Key;
        }

        int removed = 0;
        for (int i = 0; i < n; i++)
        {
            if (!_chunks.TryRemove(_pruneKeys[i], out FoodChunk fc)) continue;
            removed += fc.Count;
        }

        if (removed != 0)
        {
            FoodCount -= removed;
            Revision++;
        }
    }

    private void EnsureKeyBuffer(int count)
    {
        if (_pruneKeys.Length < count) _pruneKeys = new long[math.ceilpow2(count)];
    }

    public bool TryFindNearestFood(float worldX, float worldZ, float radiusWorld,
        out long worldCell, out float distance)
    {
        worldCell = 0L;
        distance  = -1f;

        if (_map == null || radiusWorld <= 0f) return false;

        float r2 = radiusWorld * radiusWorld;

        int minCellX = Mathf.FloorToInt((worldX - radiusWorld) * InvScale);
        int maxCellX = Mathf.FloorToInt((worldX + radiusWorld) * InvScale);
        int minCellZ = Mathf.FloorToInt((worldZ - radiusWorld) * InvScale);
        int maxCellZ = Mathf.FloorToInt((worldZ + radiusWorld) * InvScale);

        int minChunkX = minCellX >> ChunkShift, maxChunkX = maxCellX >> ChunkShift;
        int minChunkZ = minCellZ >> ChunkShift, maxChunkZ = maxCellZ >> ChunkShift;

        float bestSqr = float.MaxValue;
        long  bestCell = 0L;
        bool  found = false;

        for (int chz = minChunkZ; chz <= maxChunkZ; chz++)
        for (int chx = minChunkX; chx <= maxChunkX; chx++)
        {
            var fc = Resolve(Position2Int.Pack(chx, chz), out _);
            if (fc == null || fc.Count == 0) continue;

            int baseX = chx << ChunkShift;
            int baseZ = chz << ChunkShift;

            int lz0 = minCellZ - baseZ; if (lz0 < 0) lz0 = 0;
            int lz1 = maxCellZ - baseZ; if (lz1 > Size - 1) lz1 = Size - 1;

            int lx0 = minCellX - baseX; if (lx0 < 0) lx0 = 0;
            int lx1 = maxCellX - baseX; if (lx1 > Size - 1) lx1 = Size - 1;
            if (lx0 > lx1 || lz0 > lz1) continue;

            ulong xMask = lx1 - lx0 == 63
                ? ulong.MaxValue
                : (((1UL << (lx1 - lx0 + 1)) - 1UL) << lx0);

            for (int lz = lz0; lz <= lz1; lz++)
            {
                ulong row = fc.Rows[lz] & xMask;
                if (row == 0UL) continue;

                float wz = (baseZ + lz) * MapGenerator.scale + HalfCell - worldZ;
                float dz2 = wz * wz;
                if (dz2 > r2 || dz2 >= bestSqr) continue;

                while (row != 0UL)
                {
                    int lx = math.tzcnt(row);
                    row &= row - 1UL;

                    float wx = (baseX + lx) * MapGenerator.scale + HalfCell - worldX;
                    float sqr = wx * wx + dz2;
                    if (sqr > r2 || sqr >= bestSqr) continue;

                    bestSqr  = sqr;
                    bestCell = Position2Int.Pack(baseX + lx, baseZ + lz);
                    found    = true;
                }
            }
        }

        if (!found) return false;

        worldCell = bestCell;
        distance  = Mathf.Sqrt(bestSqr);
        return true;
    }

    private bool TryLocate(long cell, out FoodChunk fc, out ChunkData cd, out int idx, out FoodKind kind)
    {
        int cellX = Position2Int.GetX(cell);
        int cellZ = Position2Int.GetY(cell);
        kind = FoodKind.Plant;
        idx  = (cellZ & ChunkMask) * Size + (cellX & ChunkMask);

        fc = Resolve(Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift), out cd);
        if (fc == null) return false;
        if (!cd.TryGetObject(idx, out ObjectInstInfo info) || info.PrefabID != FoodPrefabId) return false;

        kind = (FoodKind)math.clamp(info.Amount, 0, (int)FoodKind.Meat);
        return true;
    }

    public bool TryGetKind(long cell, out FoodKind kind, out bool infected)
    {
        infected = false;
        if (!TryLocate(cell, out var fc, out _, out int idx, out kind)) return false;
        infected = (fc.Infected[idx / Size] & (1UL << (idx & ChunkMask))) != 0UL;
        return true;
    }

    private float NutritionOf(FoodChunk fc, int idx, FoodKind kind, long cell)
    {
        var r = SimulationRules.Active;
        switch (kind)
        {
            case FoodKind.Large: return r.LargeFoodEnergy;
            case FoodKind.Meat:  return _meat.TryGetValue(cell, out float e) ? e : 0f;
        }

        float maturity = fc.Maturity[idx] * (1f / 255f);
        float energy   = r.EatEnergyFood * math.lerp(r.FoodMinNutritionFraction, 1f, maturity);
        return kind == FoodKind.Toxic ? energy * r.ToxicNutritionMul : energy;
    }

    public bool TryClaim(long cell, IFoodReceiver eater, double now, out FoodClaim claim)
    {
        claim = default;
        if (!TryLocate(cell, out var fc, out var cd, out int idx, out FoodKind kind)) return false;

        claim.Kind = kind;

        if (kind == FoodKind.Large)
        {
            double window = SimulationRules.Active.LargeFoodWindow;
            int slot = FindClaim(cell);
            if (slot < 0 || ReferenceEquals(_claims[slot].Eater, eater) || now - _claims[slot].Time > window)
            {
                if (slot < 0) slot = AddClaim();
                _claims[slot] = new LargeClaim { Cell = cell, Time = now, Eater = eater };
                claim.Pending = true;
                return true;
            }

            var partner = _claims[slot].Eater;
            RemoveClaimAt(slot);

            float share = NutritionOf(fc, idx, kind, cell) * 0.5f;
            if (!RemoveAt(fc, cd, idx, cell, out claim.Infected)) return false;
            partner?.ReceiveFood(share);
            claim.Energy = share;
            return true;
        }

        claim.Energy = NutritionOf(fc, idx, kind, cell);
        return RemoveAt(fc, cd, idx, cell, out claim.Infected);
    }

    public bool TryTakePayload(long cell, out ViralPayload payload) => _payloads.TryRemove(cell, out payload);

    private bool RemoveAt(FoodChunk fc, ChunkData cd, int idx, long cell, out bool infected)
    {
        int lz = idx / Size;
        int lx = idx & ChunkMask;
        infected = (fc.Infected[lz] & (1UL << lx)) != 0UL;

        if (!cd.RemoveObject(idx)) return false;

        fc.Rows[lz]     &= ~(1UL << lx);
        fc.Infected[lz] &= ~(1UL << lx);
        fc.Maturity[idx] = 0;
        fc.Count--;
        fc.BuiltVersion = cd.Version;

        _meat.Remove(cell);
        if (!infected) _payloads.Remove(cell);

        FoodCount--;
        Revision++;
        return true;
    }

    public bool TryConsumeFood(long cell)
    {
        if (!TryLocate(cell, out var fc, out var cd, out int idx, out _)) return false;
        return RemoveAt(fc, cd, idx, cell, out _);
    }

    public bool TryAddFood(int cellX, int cellZ, FoodKind kind)
    {
        if (_map == null) return false;

        long key = Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift);
        var fc = Resolve(key, out ChunkData cd);
        if (fc == null) return false;
        return AddAt(fc, cd, cellX, cellZ, kind);
    }

    private bool AddAt(FoodChunk fc, ChunkData cd, int cellX, int cellZ, FoodKind kind)
    {
        int lx  = cellX & ChunkMask;
        int lz  = cellZ & ChunkMask;
        int idx = lz * Size + lx;

        if (cd.Occupancy[idx] != 0) return false;

        float h = cd.HeightRaw[idx];
        if (h <= WaterLevel) return false;

        var info = new ObjectInstInfo(
            new Vector3(cellX * MapGenerator.scale + HalfCell, h, cellZ * MapGenerator.scale + HalfCell),
            FoodPrefabId, (int)kind);

        if (!cd.TryAddObject(idx, in info)) return false;

        fc.Rows[lz] |= 1UL << lx;
        fc.Maturity[idx] = 0;
        fc.Count++;
        fc.BuiltVersion = cd.Version;

        FoodCount++;
        Revision++;
        return true;
    }

    public bool TryAddCorpse(float2 position, float energy, in ViralPayload payload, bool hasPayload)
    {
        if (_map == null || energy <= SimulationRules.Active.CorpseMinEnergy) return false;

        int cx = Mathf.FloorToInt(position.x * InvScale);
        int cz = Mathf.FloorToInt(position.y * InvScale);
        int search = math.max(0, SimulationRules.Active.CorpseSearchCells);

        for (int ring = 0; ring <= search; ring++)
        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            if (math.max(math.abs(dx), math.abs(dz)) != ring) continue;

            int x = cx + dx, z = cz + dz;
            var fc = Resolve(Position2Int.Pack(x >> ChunkShift, z >> ChunkShift), out ChunkData cd);
            if (fc == null || !AddAt(fc, cd, x, z, FoodKind.Meat)) continue;

            long cell = Position2Int.Pack(x, z);
            _meat[cell] = energy;
            if (hasPayload && payload.Codons != null && payload.Count > 0)
            {
                int idx = (z & ChunkMask) * Size + (x & ChunkMask);
                fc.Infected[idx / Size] |= 1UL << (idx & ChunkMask);
                _payloads[cell] = payload;
            }
            return true;
        }
        return false;
    }

    public void TickEcology(float dt, double now, float season, int maxFood)
    {
        if (_map == null || dt <= 0f) return;
        var r = SimulationRules.Active;

        EnsureKeyBuffer(_chunks.Count);
        int n = 0;
        foreach (var kv in _chunks) _pruneKeys[n++] = kv.Key;

        float growth   = r.FoodGrowthRate;
        float seed     = r.FoodSeedFraction;
        float matSteps = r.FoodMaturityPerSecond * 255f * dt;
        int   burst    = math.max(1, r.FoodRegenBurst);

        for (int k = 0; k < n; k++)
        {
            long key = _pruneKeys[k];
            var fc = Resolve(key, out ChunkData cd);
            if (fc == null) continue;

            if (fc.Fertility < 0f) fc.Fertility = ComputeFertility(cd, r);

            fc.MaturityBudget += matSteps;
            int steps = (int)fc.MaturityBudget;
            if (steps > 0)
            {
                fc.MaturityBudget -= steps;
                AgeFood(fc, steps);
            }

            float K = r.FoodChunkCapacity * fc.Fertility * season;
            int   N = fc.Count;
            if (K <= 0f || N >= K || FoodCount >= maxFood) { fc.GrowthBudget = 0f; continue; }

            fc.GrowthBudget += (growth * N * (1f - N / K) + growth * K * seed) * dt;
            int spawn = math.min((int)fc.GrowthBudget, burst);
            if (spawn <= 0) continue;
            fc.GrowthBudget -= spawn;

            int baseX = Position2Int.GetX(key) << ChunkShift;
            int baseZ = Position2Int.GetY(key) << ChunkShift;
            for (int s = 0; s < spawn && FoodCount < maxFood; s++)
            {
                int lx = RavineRandom.RangeInt(0, Size);
                int lz = RavineRandom.RangeInt(0, Size);
                AddAt(fc, cd, baseX + lx, baseZ + lz, RollKind(cd.BiomeMap[lz * Size + lx], r));
            }
            if (fc.GrowthBudget > burst) fc.GrowthBudget = burst;
        }

        TickMeat(dt, r);
        ExpireClaims(now, r.LargeFoodWindow);
    }

    public FoodKind RollKind(int biome, SimulationRules r)
    {
        float roll = RavineRandom.RangeFloat();
        float toxic = r.BiomeToxicity(biome);
        if (roll < toxic) return FoodKind.Toxic;
        if (roll < toxic + r.LargeFoodChance) return FoodKind.Large;
        return FoodKind.Plant;
    }

    private static float ComputeFertility(ChunkData cd, SimulationRules r)
    {
        float sum = 0f;
        int total = ChunkData.TotalCells;
        for (int i = 0; i < total; i++) sum += r.BiomeFertility(cd.BiomeMap[i]);
        return sum / total;
    }

    private static void AgeFood(FoodChunk fc, int steps)
    {
        for (int lz = 0; lz < Size; lz++)
        {
            ulong row = fc.Rows[lz];
            while (row != 0UL)
            {
                int lx = math.tzcnt(row);
                row &= row - 1UL;
                int idx = lz * Size + lx;
                fc.Maturity[idx] = (byte)math.min(255, fc.Maturity[idx] + steps);
            }
        }
    }

    private void TickMeat(float dt, SimulationRules r)
    {
        if (_meat.Count == 0) return;

        float keep = math.exp(-dt / math.max(r.CorpseSpoilTau, 1e-3f));
        float min  = r.CorpseMinEnergy;

        EnsureKeyBuffer(_meat.Count);
        int n = 0;
        foreach (var kv in _meat) _pruneKeys[n++] = kv.Key;

        for (int i = 0; i < n; i++)
        {
            long cell = _pruneKeys[i];
            if (!_meat.TryGetValue(cell, out float e)) continue;
            e *= keep;
            if (e >= min) { _meat[cell] = e; continue; }

            _meat.Remove(cell);
            _payloads.Remove(cell);
            TryConsumeFood(cell);
        }
    }

    private int FindClaim(long cell)
    {
        for (int i = 0; i < _claimCount; i++)
            if (_claims[i].Cell == cell) return i;
        return -1;
    }

    private int AddClaim()
    {
        if (_claimCount == _claims.Length) System.Array.Resize(ref _claims, _claims.Length << 1);
        return _claimCount++;
    }

    private void RemoveClaimAt(int i)
    {
        int last = --_claimCount;
        _claims[i]    = _claims[last];
        _claims[last] = default;
    }

    private void ExpireClaims(double now, double window)
    {
        for (int i = _claimCount - 1; i >= 0; i--)
            if (now - _claims[i].Time > window) RemoveClaimAt(i);
    }
}
