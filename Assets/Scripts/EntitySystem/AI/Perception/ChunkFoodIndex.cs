using UnityEngine;
using Unity.Mathematics;
using TheRavine.Extensions;
using TheRavine.Generator;

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

    public int FoodCount { get; private set; }
    public int Revision  { get; private set; }

    public ChunkFoodIndex(MapGenerator map) => _map = map;

    private sealed class FoodChunk
    {
        public readonly ulong[] Rows = new ulong[Size];
        public int Count;
        public int BuiltVersion = -1;
        public ChunkData Source;
    }

    private long[] _pruneKeys = new long[16];

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

        if (_pruneKeys.Length < _chunks.Count)
            _pruneKeys = new long[math.ceilpow2(_chunks.Count)];

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

    public static float2 CellCenter(long cell)
        => new float2(Position2Int.GetX(cell) * MapGenerator.scale + HalfCell,
                      Position2Int.GetY(cell) * MapGenerator.scale + HalfCell);

    public bool TryConsumeFood(long cell)
    {
        int cellX = Position2Int.GetX(cell);
        int cellZ = Position2Int.GetY(cell);
        long key  = Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift);

        var fc = Resolve(key, out ChunkData cd);
        if (fc == null) return false;

        int lx  = cellX & ChunkMask;
        int lz  = cellZ & ChunkMask;
        int idx = lz * Size + lx;

        if (!cd.TryGetObject(idx, out ObjectInstInfo info)) return false;
        if (info.PrefabID != FoodPrefabId) return false;
        if (!cd.RemoveObject(idx)) return false;

        fc.Rows[lz] &= ~(1UL << lx);
        fc.Count--;
        fc.BuiltVersion = cd.Version;

        FoodCount--;
        Revision++;
        return true;
    }

    public bool TryAddFood(int cellX, int cellZ, int amount)
    {
        if (_map == null) return false;

        long key = Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift);
        var fc = Resolve(key, out ChunkData cd);
        if (fc == null) return false;

        int lx  = cellX & ChunkMask;
        int lz  = cellZ & ChunkMask;
        int idx = lz * Size + lx;

        if (cd.Occupancy[idx] != 0) return false;

        float h = cd.HeightRaw[idx];
        if (h <= WaterLevel) return false;

        var info = new ObjectInstInfo(
            new Vector3(cellX * MapGenerator.scale + HalfCell, h, cellZ * MapGenerator.scale + HalfCell),
            FoodPrefabId, amount);

        if (!cd.TryAddObject(idx, in info)) return false;

        fc.Rows[lz] |= 1UL << lx;
        fc.Count++;
        fc.BuiltVersion = cd.Version;

        FoodCount++;
        Revision++;
        return true;
    }
}