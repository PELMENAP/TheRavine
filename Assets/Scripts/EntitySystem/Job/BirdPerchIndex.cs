using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

namespace TheRavine.EntityControl
{
    public struct TreePerch
    {
        public float2 Xz;
        public float YMin, YMax, Radius;
        public ulong Slots;
    }

    public unsafe struct ChunkObjects
    {
        public ObjectInstInfo* Ptr;
        public int Length;
    }

    public struct BirdPerchGrid
    {
        [ReadOnly] public NativeArray<TreePerch> Trees;
        [ReadOnly] public NativeArray<int> CellEnd;
        [ReadOnly] public NativeArray<int> Items;
        public int Mask;
        public float InvCell;

        public int Start(int bucket) => bucket == 0 ? 0 : CellEnd[bucket - 1];
        public int End(int bucket) => CellEnd[bucket];
        public int Bucket(int2 cell) => BoidHash.Bucket(cell, Mask);
        public int2 Cell(float2 xz) => (int2)math.floor(xz * InvCell);

        public int FindExact(float2 xz)
        {
            int b = Bucket(Cell(xz));
            int end = End(b);
            for (int k = Start(b); k < end; k++)
            {
                int t = Items[k];
                if (math.all(Trees[t].Xz == xz)) return t;
            }
            return -1;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public unsafe struct PerchCollectIdsJob : IJob
    {
        [ReadOnly] public NativeArray<ChunkObjects> Chunks;
        [ReadOnly] public NativeHashMap<int, int> Known;
        public NativeHashSet<int> Unknown;

        public void Execute()
        {
            Unknown.Clear();
            for (int c = 0; c < Chunks.Length; c++)
            {
                ChunkObjects co = Chunks[c];
                for (int i = 0; i < co.Length; i++)
                {
                    int id = co.Ptr[i].PrefabID;
                    if (id >= 0 && !Known.ContainsKey(id)) Unknown.Add(id);
                }
            }
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public unsafe struct PerchBuildJob : IJob
    {
        [ReadOnly] public NativeArray<ChunkObjects> Chunks;
        [ReadOnly] public NativeHashMap<int, int> Variants;
        [ReadOnly] public NativeHashMap<int, float4> Dims;
        public NativeList<TreePerch> Trees;
        public NativeArray<float> MaxRadius;

        public void Execute()
        {
            Trees.Clear();
            float maxR = 0f;
            for (int c = 0; c < Chunks.Length; c++)
            {
                ChunkObjects co = Chunks[c];
                for (int i = 0; i < co.Length; i++)
                {
                    ref ObjectInstInfo o = ref co.Ptr[i];
                    if (!Variants.TryGetValue(o.PrefabID, out int vc) || vc <= 0) continue;

                    float3 p = o.Position;
                    int seed = (int)(math.hash(p) & 0x7FFFFFFFu);
                    if (!Dims.TryGetValue((o.PrefabID << 8) | (seed % vc), out float4 d)) continue;

                    Trees.Add(new TreePerch
                    {
                        Xz = p.xz,
                        YMin = p.y + d.y,
                        YMax = p.y + d.z,
                        Radius = d.x,
                        Slots = 0ul
                    });
                    maxR = math.max(maxR, d.x);
                }
            }
            MaxRadius[0] = maxR;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct PerchBucketJob : IJob
    {
        [ReadOnly] public NativeArray<TreePerch> Trees;
        public NativeArray<int> CellEnd, Items, BucketOf;
        public int Mask;
        public float InvCell;

        public void Execute()
        {
            int buckets = Mask + 1;
            for (int b = 0; b <= buckets; b++) CellEnd[b] = 0;

            for (int i = 0; i < Trees.Length; i++)
            {
                int b = BoidHash.Bucket((int2)math.floor(Trees[i].Xz * InvCell), Mask);
                BucketOf[i] = b;
                CellEnd[b + 1]++;
            }

            for (int b = 0; b < buckets; b++) CellEnd[b + 1] += CellEnd[b];

            for (int i = 0; i < Trees.Length; i++)
                Items[CellEnd[BucketOf[i]]++] = i;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct PerchRemapJob : IJob
    {
        public BirdPerchGrid Grid;
        [NativeDisableContainerSafetyRestriction] public NativeArray<TreePerch> Trees;
        [ReadOnly] public NativeArray<TreePerch> Previous;
        public NativeArray<int> PerchRef;
        public NativeArray<byte> State;
        public NativeArray<float> Timer;

        public void Execute()
        {
            for (int i = 0; i < PerchRef.Length; i++)
            {
                int r = PerchRef[i];
                if (r < 0) continue;

                int old = r >> BirdState.SlotShift;
                int slot = r & BirdState.SlotMask;
                int t = old < Previous.Length ? Grid.FindExact(Previous[old].Xz) : -1;

                if (t >= 0)
                {
                    TreePerch tree = Trees[t];
                    tree.Slots |= 1ul << slot;
                    Trees[t] = tree;
                    PerchRef[i] = (t << BirdState.SlotShift) | slot;
                    continue;
                }

                PerchRef[i] = BirdState.PerchNone;
                byte s = State[i];
                if (s == BirdState.Perched || s == BirdState.Landing)
                {
                    State[i] = BirdState.TakeOff;
                    Timer[i] = 0f;
                }
                else if (s == BirdState.Approach)
                {
                    State[i] = BirdState.Flying;
                    Timer[i] = 0f;
                }
            }
        }
    }

    public sealed class BirdPerchIndex : IDisposable
    {
        private NativeList<TreePerch> _trees, _previous;
        private NativeArray<int> _cellEnd, _items, _bucketOf;
        private NativeArray<ChunkObjects> _chunks;
        private NativeHashMap<int, int> _variants;
        private NativeHashMap<int, float4> _dims;
        private NativeHashSet<int> _unknown;
        private NativeArray<float> _maxRadius;

        private readonly float _baseCell, _margin;
        private int _capacity, _mask;
        private float _invCell = 1f;

        public BirdPerchIndex(int chunkSide, float cellSize, float margin)
        {
            _baseCell = math.max(cellSize, 0.5f);
            _margin = margin;
            _trees = new NativeList<TreePerch>(256, Allocator.Persistent);
            _previous = new NativeList<TreePerch>(256, Allocator.Persistent);
            _chunks = new NativeArray<ChunkObjects>(chunkSide * chunkSide, Allocator.Persistent);
            _variants = new NativeHashMap<int, int>(64, Allocator.Persistent);
            _dims = new NativeHashMap<int, float4>(256, Allocator.Persistent);
            _unknown = new NativeHashSet<int>(64, Allocator.Persistent);
            _maxRadius = new NativeArray<float>(1, Allocator.Persistent);
            EnsureCapacity(256);
        }

        public NativeArray<TreePerch> Trees => _trees.AsArray();

        public BirdPerchGrid Grid => new()
        {
            Trees = _trees.AsArray(),
            CellEnd = _cellEnd,
            Items = _items,
            Mask = _mask,
            InvCell = _invCell
        };

        private void EnsureCapacity(int count)
        {
            if (count <= _capacity && _cellEnd.IsCreated) return;
            int capacity = math.ceilpow2(math.max(count, 16));
            if (_cellEnd.IsCreated) _cellEnd.Dispose();
            if (_items.IsCreated) _items.Dispose();
            if (_bucketOf.IsCreated) _bucketOf.Dispose();
            _capacity = capacity;
            _mask = capacity * 2 - 1;
            _cellEnd = new NativeArray<int>(_mask + 2, Allocator.Persistent);
            _items = new NativeArray<int>(capacity, Allocator.Persistent);
            _bucketOf = new NativeArray<int>(capacity, Allocator.Persistent);
        }

        public unsafe void Rebuild(MapGenerator map, ObjectSystem objects, int2 chunkOrigin, int chunkSide,
            NativeArray<int> perchRef, NativeArray<byte> state, NativeArray<float> timer)
        {
            (_trees, _previous) = (_previous, _trees);

            for (int z = 0; z < chunkSide; z++)
            for (int x = 0; x < chunkSide; x++)
            {
                ChunkObjects co = default;
                if (map != null &&
                    map.TryGetChunk(Position2Int.Pack(chunkOrigin.x + x, chunkOrigin.y + z), out ChunkData cd) &&
                    cd.Objects.IsCreated)
                {
                    co.Ptr = (ObjectInstInfo*)cd.Objects.GetUnsafeReadOnlyPtr();
                    co.Length = cd.Objects.Length;
                }
                _chunks[z * chunkSide + x] = co;
            }

            new PerchCollectIdsJob { Chunks = _chunks, Known = _variants, Unknown = _unknown }.Run();
            if (!_unknown.IsEmpty) ResolvePrefabs(objects);

            new PerchBuildJob
            {
                Chunks = _chunks,
                Variants = _variants,
                Dims = _dims,
                Trees = _trees,
                MaxRadius = _maxRadius
            }.Run();

            EnsureCapacity(_trees.Length);
            _invCell = 1f / math.max(_baseCell, _maxRadius[0] + _margin);

            new PerchBucketJob
            {
                Trees = _trees.AsArray(),
                CellEnd = _cellEnd,
                Items = _items,
                BucketOf = _bucketOf,
                Mask = _mask,
                InvCell = _invCell
            }.Run();

            if (perchRef.IsCreated)
            {
                new PerchRemapJob
                {
                    Grid = Grid,
                    Trees = _trees.AsArray(),
                    Previous = _previous.AsArray(),
                    PerchRef = perchRef,
                    State = state,
                    Timer = timer
                }.Run();
            }
        }

        private void ResolvePrefabs(ObjectSystem objects)
        {
            foreach (int id in _unknown)
            {
                ObjectInfo info = objects != null ? objects.GetInfo(id) : null;
                if (info == null || info.LodSet is not TreeMeshCollection trees || trees.VariantCount <= 0)
                {
                    _variants[id] = 0;
                    continue;
                }

                int count = math.min(trees.VariantCount, 256);
                for (int v = 0; v < count; v++)
                {
                    Mesh mesh = trees.GetMesh(v, 0);
                    if (mesh == null) continue;
                    Bounds b = mesh.bounds;
                    _dims[(id << 8) | v] = new float4(math.max(b.extents.x, b.extents.z), b.min.y, b.max.y, 0f);
                }
                _variants[id] = count;
            }
        }

        public void Dispose()
        {
            if (_trees.IsCreated) _trees.Dispose();
            if (_previous.IsCreated) _previous.Dispose();
            if (_cellEnd.IsCreated) _cellEnd.Dispose();
            if (_items.IsCreated) _items.Dispose();
            if (_bucketOf.IsCreated) _bucketOf.Dispose();
            if (_chunks.IsCreated) _chunks.Dispose();
            if (_variants.IsCreated) _variants.Dispose();
            if (_dims.IsCreated) _dims.Dispose();
            if (_unknown.IsCreated) _unknown.Dispose();
            if (_maxRadius.IsCreated) _maxRadius.Dispose();
        }
    }
}
