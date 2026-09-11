using UnityEngine;
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
    private const int   MaxScanRadiusCells = 64;

    private readonly MapGenerator _map;

    private int       _cacheChunkX;
    private int       _cacheChunkZ;
    private ChunkData _cacheChunk;
    private bool      _cacheValid;

    private float _originX;
    private float _originZ;
    private float _radiusSqr;
    private float _bestSqr;
    private long  _bestCell;

    public int FoodCount { get; private set; }

    public ChunkFoodIndex(MapGenerator map) => _map = map;

    public bool TryFindNearestFood(float worldX, float worldZ, float radiusWorld,
        out long worldCell, out float distance)
    {
        worldCell = 0L;
        distance  = -1f;

        if (_map == null || radiusWorld <= 0f) return false;

        _cacheValid = false;
        _originX    = worldX;
        _originZ    = worldZ;
        _radiusSqr  = radiusWorld * radiusWorld;
        _bestSqr    = float.MaxValue;
        _bestCell   = 0L;

        int maxR = (int)(radiusWorld * InvScale);
        if (maxR > MaxScanRadiusCells) maxR = MaxScanRadiusCells;

        int cellX = Mathf.FloorToInt(worldX * InvScale);
        int cellZ = Mathf.FloorToInt(worldZ * InvScale);

        bool found = TestCell(cellX, cellZ);

        for (int r = 1; r <= maxR && !found; r++)
        {
            int minZ = cellZ - r;
            int maxZ = cellZ + r;

            for (int dx = -r; dx <= r; dx++)
            {
                int x = cellX + dx;
                if (TestCell(x, minZ)) found = true;
                if (TestCell(x, maxZ)) found = true;
            }

            int minX = cellX - r;
            int maxX = cellX + r;

            for (int dz = -r + 1; dz <= r - 1; dz++)
            {
                int z = cellZ + dz;
                if (TestCell(minX, z)) found = true;
                if (TestCell(maxX, z)) found = true;
            }
        }

        if (!found) return false;

        worldCell = _bestCell;
        distance  = Mathf.Sqrt(_bestSqr);
        return true;
    }

    public bool TryConsumeFood(long cell)
    {
        int cellX = Position2Int.GetX(cell);
        int cellZ = Position2Int.GetY(cell);

        if (!_map.TryGetChunk(
                Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift),
                out ChunkData cd) || cd == null)
            return false;

        int idx = (cellZ & ChunkMask) * Size + (cellX & ChunkMask);

        if (!cd.TryGetObject(idx, out ObjectInstInfo info)) return false;
        if (info.PrefabID != FoodPrefabId) return false;
        if (!cd.RemoveObject(idx)) return false;

        FoodCount--;
        return true;
    }

    public bool TryAddFood(int cellX, int cellZ, int amount)
    {
        if (_map == null) return false;

        if (!_map.TryGetChunk(
                Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift),
                out ChunkData cd) || cd == null)
            return false;

        int idx = (cellZ & ChunkMask) * Size + (cellX & ChunkMask);
        if (cd.Occupancy[idx] != 0) return false;

        float h = cd.HeightRaw[idx];
        if (h <= WaterLevel) return false;

        var info = new ObjectInstInfo(
            new Vector3(cellX * MapGenerator.scale + HalfCell, h, cellZ * MapGenerator.scale + HalfCell),
            FoodPrefabId, amount);

        if (!cd.TryAddObject(idx, in info)) return false;

        FoodCount++;
        return true;
    }

    private bool TestCell(int cellX, int cellZ)
    {
        int chunkX = cellX >> ChunkShift;
        int chunkZ = cellZ >> ChunkShift;

        if (!_cacheValid || chunkX != _cacheChunkX || chunkZ != _cacheChunkZ)
        {
            _cacheChunkX = chunkX;
            _cacheChunkZ = chunkZ;
            _cacheValid  = true;
            _map.TryGetChunk(Position2Int.Pack(chunkX, chunkZ), out _cacheChunk);
        }

        ChunkData cd = _cacheChunk;
        if (cd == null) return false;

        int idx = (cellZ & ChunkMask) * Size + (cellX & ChunkMask);
        if (cd.Occupancy[idx] == 0) return false;

        if (!cd.TryGetObject(idx, out ObjectInstInfo info)) return false;
        if (info.PrefabID != FoodPrefabId) return false;

        float dx = cellX * MapGenerator.scale + HalfCell - _originX;
        float dz = cellZ * MapGenerator.scale + HalfCell - _originZ;
        float sqr = dx * dx + dz * dz;

        if (sqr > _radiusSqr || sqr >= _bestSqr) return false;

        _bestSqr  = sqr;
        _bestCell = Position2Int.Pack(cellX, cellZ);
        return true;
    }
}