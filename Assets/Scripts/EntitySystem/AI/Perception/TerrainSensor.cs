using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public sealed class TerrainSensor
{
    private const int   Size        = MapGenerator.mapChunkSize;
    private const int   ChunkShift  = 6;
    private const int   ChunkMask   = Size - 1;
    private const int   MaxRing     = 16;
    private const float InvScale    = 1f / MapGenerator.scale;
    private const float InvMaxH     = 1f / MapGenerator.maxTerrainHeight;
    private const float InvSlopeMax = 1f / 60f;
    private const float InvWaterMax = 1f / MaxRing;
    private const float InvCost     = 1f / 255f;
    private const float InvRelSpan  = 1f / 10f;
    private const float WaterLevel  = 5f;

    private static readonly int[] RingStart = new int[MaxRing + 2];
    private static readonly int[] RingDx;
    private static readonly int[] RingDz;
    private static readonly int[] RingDzMul;

    static TerrainSensor()
    {
        int total = 0;
        for (int r = 1; r <= MaxRing; r++) total += 8 * r;

        RingDx    = new int[total];
        RingDz    = new int[total];
        RingDzMul = new int[total];

        int w = 0;
        for (int r = 1; r <= MaxRing; r++)
        {
            RingStart[r] = w;

            for (int dx = -r; dx <= r; dx++)
            {
                RingDx[w] = dx; RingDz[w] = -r; RingDzMul[w] = -r * Size; w++;
                RingDx[w] = dx; RingDz[w] =  r; RingDzMul[w] =  r * Size; w++;
            }

            for (int dz = -r + 1; dz <= r - 1; dz++)
            {
                RingDx[w] = -r; RingDz[w] = dz; RingDzMul[w] = dz * Size; w++;
                RingDx[w] =  r; RingDz[w] = dz; RingDzMul[w] = dz * Size; w++;
            }
        }
        RingStart[MaxRing + 1] = w;
    }

    private readonly MapGenerator _map;

    public TerrainSensor(MapGenerator map) => _map = map;

    public bool TrySample(float worldX, float worldZ, out TerrainSample sample)
    {
        sample = default;
        if (_map == null) return false;

        int cellX = Mathf.FloorToInt(worldX * InvScale);
        int cellZ = Mathf.FloorToInt(worldZ * InvScale);

        if (!_map.TryGetChunk(
                Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift),
                out ChunkData cd) || cd == null)
            return false;

        int lx = cellX & ChunkMask;
        int lz = cellZ & ChunkMask;
        int baseIdx = lz * Size + lx;

        float h = cd.HeightRaw[baseIdx];

        bool hasL = lx > 0;
        bool hasR = lx < Size - 1;
        bool hasD = lz > 0;
        bool hasU = lz < Size - 1;

        float hL = hasL ? cd.HeightRaw[baseIdx - 1] : h;
        float hR = hasR ? cd.HeightRaw[baseIdx + 1] : h;
        float hD = hasD ? cd.HeightRaw[baseIdx - Size] : h;
        float hU = hasU ? cd.HeightRaw[baseIdx + Size] : h;

        float spanX = ((hasL ? 1 : 0) + (hasR ? 1 : 0)) * MapGenerator.scale;
        float spanZ = ((hasD ? 1 : 0) + (hasU ? 1 : 0)) * MapGenerator.scale;

        float gx = spanX > 0f ? (hR - hL) / spanX : 0f;
        float gz = spanZ > 0f ? (hU - hD) / spanZ : 0f;

        float slopeDeg = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;

        float cost   = cd.MoveCost[baseIdx] * InvCost;
        float costPX = (hasR ? cd.MoveCost[baseIdx + 1]    : cd.MoveCost[baseIdx]) * InvCost;
        float costNX = (hasL ? cd.MoveCost[baseIdx - 1]    : cd.MoveCost[baseIdx]) * InvCost;
        float costPZ = (hasU ? cd.MoveCost[baseIdx + Size] : cd.MoveCost[baseIdx]) * InvCost;
        float costNZ = (hasD ? cd.MoveCost[baseIdx - Size] : cd.MoveCost[baseIdx]) * InvCost;

        System.Span<float> biome = stackalloc float[4];
        EncodeBiome(cd.BiomeMap[baseIdx], biome);

        sample = new TerrainSample(
            Mathf.Clamp01(h * InvMaxH),
            Mathf.Clamp01(slopeDeg * InvSlopeMax),
            Mathf.Clamp(gx, -1f, 1f),
            Mathf.Clamp(gz, -1f, 1f),
            WaterProximity(cd, lx, lz, baseIdx),
            cost, costPX, costNX, costPZ, costNZ,
            biome[0], biome[1], biome[2], biome[3],
            RingDensity(cd, lx, lz, baseIdx, 2),
            RingDensity(cd, lx, lz, baseIdx, 4),
            RingDensity(cd, lx, lz, baseIdx, 8),
            RelativeHeight(cd, lx, lz, baseIdx, h));

        return true;
    }

    private static void EncodeBiome(int biome, System.Span<float> channels)
    {
        if (biome < 0) biome = 0;

        int primary = biome & 3;
        channels[primary] = 1f;
        float sum = 1f;

        if (biome >= 4)
        {
            int secondary = (biome >> 2) & 3;
            channels[secondary] += 0.5f;
            sum += 0.5f;
        }

        float inv = 1f / sum;
        channels[0] *= inv;
        channels[1] *= inv;
        channels[2] *= inv;
        channels[3] *= inv;
    }

    private static float RingDensity(ChunkData cd, int lx, int lz, int baseIdx, int r)
    {
        int start = RingStart[r];
        int end   = RingStart[r + 1];

        int known = 0;
        int busy  = 0;

        for (int i = start; i < end; i++)
        {
            int nx = lx + RingDx[i];
            if ((uint)nx >= (uint)Size) continue;
            int nz = lz + RingDz[i];
            if ((uint)nz >= (uint)Size) continue;

            known++;
            if (cd.Occupancy[baseIdx + RingDx[i] + RingDzMul[i]] != 0) busy++;
        }

        return known > 0 ? (float)busy / known : 0f;
    }

    private static float WaterProximity(ChunkData cd, int lx, int lz, int baseIdx)
    {
        if (cd.HeightRaw[baseIdx] < WaterLevel) return 1f;

        for (int r = 1; r <= MaxRing; r++)
        {
            int start = RingStart[r];
            int end   = RingStart[r + 1];

            for (int i = start; i < end; i++)
            {
                int nx = lx + RingDx[i];
                if ((uint)nx >= (uint)Size) continue;
                int nz = lz + RingDz[i];
                if ((uint)nz >= (uint)Size) continue;

                if (cd.HeightRaw[baseIdx + RingDx[i] + RingDzMul[i]] < WaterLevel)
                    return 1f - r * InvWaterMax;
            }
        }
        return 0f;
    }

    private static float RelativeHeight(ChunkData cd, int lx, int lz, int baseIdx, float h)
    {
        float sum = 0f;
        int   cnt = 0;
        int   row = baseIdx - 4 * Size;

        for (int dz = -4; dz <= 3; dz++, row += Size)
        {
            int nz = lz + dz;
            if ((uint)nz >= (uint)Size) continue;

            for (int dx = -4; dx <= 3; dx++)
            {
                int nx = lx + dx;
                if ((uint)nx >= (uint)Size) continue;

                sum += cd.HeightRaw[row + dx];
                cnt++;
            }
        }

        if (cnt == 0) return 0f;
        return Mathf.Clamp((h - sum / cnt) * InvRelSpan, -1f, 1f);
    }
}