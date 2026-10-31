using System;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public sealed class TerrainSensor
{
    private const int   Size        = MapGenerator.mapChunkSize;
    private const int   ChunkMask   = Size - 1;
    private static readonly int ChunkShift;
    private const float InvScale    = 1f / MapGenerator.scale;
    private const float InvMaxH     = 1f / MapGenerator.maxTerrainHeight;
    private const float InvCost     = 1f / 255f;

    private static int   _ringMax;
    private static int[] RingStart = System.Array.Empty<int>();
    private static int[] RingDx    = System.Array.Empty<int>();
    private static int[] RingDz    = System.Array.Empty<int>();
    private static int[] RingDzMul = System.Array.Empty<int>();

    static TerrainSensor()
    {
        if (Size <= 0 || (Size & ChunkMask) != 0)
            throw new InvalidOperationException(
                $"TerrainSensor: mapChunkSize({Size}) must be a positive power of two");

        ChunkShift = math.tzcnt((uint)Size);
    }

    private static void EnsureRings(int maxRadius)
    {
        if (maxRadius <= _ringMax) return;

        int total = 0;
        for (int r = 1; r <= maxRadius; r++) total += 8 * r;

        var start = new int[maxRadius + 2];
        var dxs   = new int[total];
        var dzs   = new int[total];
        var dzMul = new int[total];

        int w = 0;
        for (int r = 1; r <= maxRadius; r++)
        {
            start[r] = w;

            for (int dx = -r; dx <= r; dx++)
            {
                dxs[w] = dx; dzs[w] = -r; dzMul[w] = -r * Size; w++;
                dxs[w] = dx; dzs[w] =  r; dzMul[w] =  r * Size; w++;
            }

            for (int dz = -r + 1; dz <= r - 1; dz++)
            {
                dxs[w] = -r; dzs[w] = dz; dzMul[w] = dz * Size; w++;
                dxs[w] =  r; dzs[w] = dz; dzMul[w] = dz * Size; w++;
            }
        }
        start[maxRadius + 1] = w;

        RingStart = start;
        RingDx    = dxs;
        RingDz    = dzs;
        RingDzMul = dzMul;
        _ringMax  = maxRadius;
    }

    private readonly MapGenerator _map;

    public TerrainSensor(MapGenerator map) => _map = map;

    public bool TrySample(float worldX, float worldZ, out TerrainSample sample)
    {
        sample = default;
        if (_map == null) return false;

        int cellX = (int)math.floor(worldX * InvScale);
        int cellZ = (int)math.floor(worldZ * InvScale);

        long chunkKey = Position2Int.Pack(cellX >> ChunkShift, cellZ >> ChunkShift);
        if (!_map.TryGetChunk(chunkKey, out ChunkData cd) || cd == null)
            return false;

        ref readonly var rules = ref SimulationRules.Frame;
        int rNear = math.clamp(rules.DensityRadiusNear, 1, Size);
        int rMid  = math.clamp(rules.DensityRadiusMid, 1, Size);
        int rFar  = math.clamp(rules.DensityRadiusFar, 1, Size);
        EnsureRings(math.max(rNear, math.max(rMid, rFar)));

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

        float slopeDeg = math.degrees(math.atan(math.sqrt(gx * gx + gz * gz)));

        float cost   = cd.MoveCost[baseIdx] * InvCost;
        float costPX = (hasR ? cd.MoveCost[baseIdx + 1]    : cd.MoveCost[baseIdx]) * InvCost;
        float costNX = (hasL ? cd.MoveCost[baseIdx - 1]    : cd.MoveCost[baseIdx]) * InvCost;
        float costPZ = (hasU ? cd.MoveCost[baseIdx + Size] : cd.MoveCost[baseIdx]) * InvCost;
        float costNZ = (hasD ? cd.MoveCost[baseIdx - Size] : cd.MoveCost[baseIdx]) * InvCost;

        System.Span<float> biome = stackalloc float[4];
        EncodeBiome(cd.BiomeMap[baseIdx], biome);

        sample = new TerrainSample(
            math.saturate(h * InvMaxH),
            math.saturate(slopeDeg * math.rcp(rules.SlopeMaxDeg)),
            math.clamp(gx, -1f, 1f),
            math.clamp(gz, -1f, 1f),
            WaterProximity(chunkKey, cd, baseIdx, in rules),
            cost, costPX, costNX, costPZ, costNZ,
            biome[0], biome[1], biome[2], biome[3],
            RingDensity(cd, lx, lz, baseIdx, rNear),
            RingDensity(cd, lx, lz, baseIdx, rMid),
            RingDensity(cd, lx, lz, baseIdx, rFar),
            RelativeHeight(cd, lx, lz, baseIdx, h, rules.RelativeHeightRadius, rules.RelSpan));

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

    private float WaterProximity(long chunkKey, ChunkData cd, int baseIdx, in SimulationRules.RulesFrame rules)
    {
        int ring = math.clamp(rules.MaxRing, 1, Size);
        _map.EnsureWaterDistance(chunkKey, cd, rules.WaterLevel, ring);

        int d = cd.WaterDistance[baseIdx];
        if (d == 0) return 1f;
        return d <= ring ? 1f - d * (1f / ring) : 0f;
    }

    private static float RelativeHeight(ChunkData cd, int lx, int lz, int baseIdx, float h, int radius, float span)
    {
        radius = math.clamp(radius, 1, Size);

        float sum = 0f;
        int   cnt = 0;
        int   row = baseIdx - radius * Size;

        for (int dz = -radius; dz < radius; dz++, row += Size)
        {
            int nz = lz + dz;
            if ((uint)nz >= (uint)Size) continue;

            for (int dx = -radius; dx < radius; dx++)
            {
                int nx = lx + dx;
                if ((uint)nx >= (uint)Size) continue;

                sum += cd.HeightRaw[row + dx];
                cnt++;
            }
        }

        if (cnt == 0) return 0f;
        return math.clamp((h - sum / cnt) * math.rcp(span), -1f, 1f);
    }
}