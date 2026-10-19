using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using TheRavine.Extensions;
using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public struct BirdHeightSampler
    {
        public const byte Valid = 1, Perchable = 2;

        [ReadOnly] public NativeArray<float> Terrain;
        [ReadOnly] public NativeArray<float> Canopy;
        [ReadOnly] public NativeArray<byte> Flags;
        public float2 Origin;
        public float InvStep;
        public int Size;

        public float SampleTerrain(float2 xz) => Bilinear(Terrain, xz);
        public float SampleCanopy(float2 xz) => Bilinear(Canopy, xz);

        public bool IsPerchable(float2 xz)
        {
            int2 c = (int2)math.round((xz - Origin) * InvStep);
            if (math.any(c < 0) || math.any(c >= Size)) return false;
            return (Flags[c.y * Size + c.x] & Perchable) != 0;
        }

        private float Bilinear(NativeArray<float> a, float2 xz)
        {
            float2 g = math.clamp((xz - Origin) * InvStep, 0f, Size - 1.001f);
            int2 i = (int2)g;
            float2 t = g - i;
            int k = i.y * Size + i.x;
            return math.lerp(math.lerp(a[k], a[k + 1], t.x), math.lerp(a[k + Size], a[k + Size + 1], t.x), t.y);
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public unsafe struct BirdHeightBuildJob : IJob
    {
        public NativeArray<float> Terrain, Canopy;
        public NativeArray<byte> Flags;
        public NativeArray<float> Fallback;
        [ReadOnly] public NativeArray<long> HeightPtrs, RiverPtrs;
        public int2 ChunkOrigin;
        public int ChunkSide;
        public int2 OriginCell;
        public int CellStep, Size;
        public float MinNormalY, WaterThreshold;

        public void Execute()
        {
            float last = Fallback[0];
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int k = z * Size + x;
                int cx = OriginCell.x + x * CellStep;
                int cz = OriginCell.y + z * CellStep;

                if (!TryRaw(HeightPtrs, cx, cz, out float h))
                {
                    Terrain[k] = last;
                    Canopy[k] = last;
                    Flags[k] = 0;
                    continue;
                }

                last = h;
                Terrain[k] = h;
                Canopy[k] = h;

                float hL = TryRaw(HeightPtrs, cx - 1, cz, out float a) ? a : h;
                float hR = TryRaw(HeightPtrs, cx + 1, cz, out float b) ? b : h;
                float hD = TryRaw(HeightPtrs, cx, cz - 1, out float c) ? c : h;
                float hU = TryRaw(HeightPtrs, cx, cz + 1, out float d) ? d : h;
                float3 n = MapGenerator.NormalFromHeights(hL, hR, hD, hU);
                float river = TryRaw(RiverPtrs, cx, cz, out float r) ? r : 1f;

                byte flags = BirdHeightSampler.Valid;
                if (n.y >= MinNormalY && river <= WaterThreshold) flags |= BirdHeightSampler.Perchable;
                Flags[k] = flags;
            }
            Fallback[0] = last;
        }

        private bool TryRaw(NativeArray<long> ptrs, int cx, int cz, out float value)
        {
            int sx = (cx >> MapGenerator.RowShift) - ChunkOrigin.x;
            int sz = (cz >> MapGenerator.RowShift) - ChunkOrigin.y;
            if ((uint)sx >= (uint)ChunkSide || (uint)sz >= (uint)ChunkSide) { value = 0f; return false; }
            long p = ptrs[sz * ChunkSide + sx];
            if (p == 0) { value = 0f; return false; }
            value = ((float*)p)[MapGenerator.CellToLocalIdx(cx, cz)];
            return true;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct BirdCanopyStampJob : IJob
    {
        public NativeArray<float> Canopy;
        [ReadOnly] public NativeArray<TreePerch> Trees;
        public float2 Origin;
        public float InvStep;
        public int Size;

        public void Execute()
        {
            for (int t = 0; t < Trees.Length; t++)
            {
                TreePerch tree = Trees[t];
                float2 g = (tree.Xz - Origin) * InvStep;
                float rg = tree.Radius * InvStep;
                int2 lo = math.max((int2)math.floor(g - rg), 0);
                int2 hi = math.min((int2)math.ceil(g + rg), Size - 1);
                float rg2 = rg * rg;

                for (int z = lo.y; z <= hi.y; z++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    if (math.lengthsq(new float2(x, z) - g) > rg2) continue;
                    int k = z * Size + x;
                    Canopy[k] = math.max(Canopy[k], tree.YMax);
                }
            }
        }
    }

    public sealed class BirdHeightField : IDisposable
    {
        private NativeArray<float> _terrain, _canopy, _fallback;
        private NativeArray<byte> _flags;
        private NativeArray<long> _heightPtrs, _riverPtrs;

        private readonly int _size, _cellStep, _chunkSide;
        private readonly float _step, _recenterDistance;
        private float2 _origin, _center;
        private int2 _chunkOrigin;

        public bool IsBuilt { get; private set; }
        public int2 ChunkOrigin => _chunkOrigin;
        public int ChunkSide => _chunkSide;

        public BirdHeightField(int size, int cellStep, float recenterFraction)
        {
            _size = math.max(size, 4);
            _cellStep = math.max(cellStep, 1);
            _step = _cellStep * MapGenerator.scale;
            _recenterDistance = 0.5f * (_size - 1) * _step * math.saturate(recenterFraction);
            _chunkSide = ((_size - 1) * _cellStep >> MapGenerator.RowShift) + 2;

            int cells = _size * _size;
            _terrain = new NativeArray<float>(cells, Allocator.Persistent);
            _canopy = new NativeArray<float>(cells, Allocator.Persistent);
            _flags = new NativeArray<byte>(cells, Allocator.Persistent);
            _fallback = new NativeArray<float>(1, Allocator.Persistent);
            _heightPtrs = new NativeArray<long>(_chunkSide * _chunkSide, Allocator.Persistent);
            _riverPtrs = new NativeArray<long>(_chunkSide * _chunkSide, Allocator.Persistent);
        }

        public BirdHeightSampler Sampler => new()
        {
            Terrain = _terrain,
            Canopy = _canopy,
            Flags = _flags,
            Origin = _origin,
            InvStep = 1f / _step,
            Size = _size
        };

        public bool NeedsRecenter(float2 viewer) =>
            !IsBuilt || math.any(math.abs(viewer - _center) > _recenterDistance);

        public unsafe void Rebuild(MapGenerator map, float2 viewer, float maxSlope, float waterThreshold)
        {
            int half = (_size - 1) / 2;
            int2 originCell = ((int2)math.floor(viewer / _step) - half) * _cellStep;
            _origin = (float2)originCell * MapGenerator.scale;
            _center = _origin + half * _step;
            _chunkOrigin = originCell >> MapGenerator.RowShift;

            for (int z = 0; z < _chunkSide; z++)
            for (int x = 0; x < _chunkSide; x++)
            {
                int s = z * _chunkSide + x;
                long hp = 0, rp = 0;
                if (map != null &&
                    map.TryGetChunk(Position2Int.Pack(_chunkOrigin.x + x, _chunkOrigin.y + z), out ChunkData cd) &&
                    cd.DetailLevel != ChunkData.NotGenerated)
                {
                    hp = (long)cd.HeightRaw.GetUnsafeReadOnlyPtr();
                    rp = (long)cd.RiverBlend.GetUnsafeReadOnlyPtr();
                }
                _heightPtrs[s] = hp;
                _riverPtrs[s] = rp;
            }

            new BirdHeightBuildJob
            {
                Terrain = _terrain,
                Canopy = _canopy,
                Flags = _flags,
                Fallback = _fallback,
                HeightPtrs = _heightPtrs,
                RiverPtrs = _riverPtrs,
                ChunkOrigin = _chunkOrigin,
                ChunkSide = _chunkSide,
                OriginCell = originCell,
                CellStep = _cellStep,
                Size = _size,
                MinNormalY = math.cos(math.radians(maxSlope)),
                WaterThreshold = waterThreshold
            }.Run();

            IsBuilt = true;
        }

        public void StampCanopy(NativeArray<TreePerch> trees)
        {
            if (trees.Length == 0) return;
            new BirdCanopyStampJob
            {
                Canopy = _canopy,
                Trees = trees,
                Origin = _origin,
                InvStep = 1f / _step,
                Size = _size
            }.Run();
        }

        public void Dispose()
        {
            if (_terrain.IsCreated) _terrain.Dispose();
            if (_canopy.IsCreated) _canopy.Dispose();
            if (_flags.IsCreated) _flags.Dispose();
            if (_fallback.IsCreated) _fallback.Dispose();
            if (_heightPtrs.IsCreated) _heightPtrs.Dispose();
            if (_riverPtrs.IsCreated) _riverPtrs.Dispose();
        }
    }
}
