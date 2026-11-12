using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Extensions;
using TheRavine.ViewControl;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public sealed class EndlessTerrain : IEndless, IViewDirectional
        {
            private const int BlockCells = MapGenerator.WindowSide * MapGenerator.mapChunkSize;
            private const float BlockWorld = MapGenerator.WindowSide * MapGenerator.chunkSize;
            private const float BoundsHeight = 1000f;
            private const int RowBatch = 8;
            private const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            private sealed class MeshLevel
            {
                public int Step;
                public int Verts;
                public int VertexCount;
                public int IndexCount;
                public float SkirtDepth;
                public NativeArray<ushort> Indices;
            }

            private sealed class MeshTarget
            {
                public MeshLevel Level;
                public Mesh Mesh;
                public Transform Transform;
                public MeshRenderer Renderer;
                public int Bx;
                public int Bz;
                public int Lod;
                public bool Active;
                public bool Dirty;
                public bool HasContent;
                public long Stamp;
            }

            private readonly MapGenerator generator;
            private readonly MeshLevel[] levels;
            private readonly MeshTarget central;
            private readonly MeshTarget[] pool;
            private readonly ViewBlock[] wanted;
            private readonly int[] order;
            private readonly Bounds localBounds;
            private readonly Mesh.MeshDataArray[] pendingData;
            private readonly int[] pendingBlocks;
            private NativeArray<JobHandle> handles;

            private long center;
            private bool hasCenter;
            private bool centralDirty;
            private int orderCount;
            private long stamp;
            private ViewSector sector;

            public int BuiltBlocks { get; private set; }

            public EndlessTerrain(MapGenerator _generator, ChunkGenerationSettings settings)
            {
                generator = _generator;
                sector = generator.Sector;
                localBounds = new Bounds(
                    new Vector3(BlockWorld * 0.5f, 0f, BlockWorld * 0.5f),
                    new Vector3(BlockWorld, BoundsHeight, BlockWorld));

                int ringCount = generator.LodRingCount;
                levels = new MeshLevel[ringCount + 1];
                levels[0] = CreateLevel(1, settings.skirtDepth);
                for (int r = 1; r <= ringCount; r++)
                    levels[r] = CreateLevel(ValidStep(settings.lodSteps[r - 1]), settings.skirtDepth);

                central = new MeshTarget
                {
                    Level = levels[0],
                    Mesh = CreateMesh("TerrainCentral"),
                    Transform = generator.terrainTransform,
                    Active = true
                };
                generator.terrainFilter.sharedMesh = central.Mesh;

                int poolSize = ringCount > 0 ? ViewWedge.MaxBlocks(ringCount) : 0;
                pool = new MeshTarget[poolSize];
                wanted = new ViewBlock[math.max(poolSize, 1)];
                order = new int[math.max(poolSize, 1)];
                pendingData = new Mesh.MeshDataArray[math.max(poolSize, 1)];
                pendingBlocks = new int[math.max(poolSize, 1)];
                handles = new NativeArray<JobHandle>(math.max(poolSize, 1), Allocator.Persistent);

                var material = generator.terrainFilter.GetComponent<MeshRenderer>().sharedMaterial;
                var root = generator.terrainTransform.parent;

                for (int i = 0; i < pool.Length; i++)
                    pool[i] = CreateBlock(i, material, root);
            }

            private MeshTarget CreateBlock(int index, Material material, Transform root)
            {
                var go = new GameObject($"TerrainBlock_{index}");
                if (root != null) go.transform.SetParent(root, false);

                Mesh mesh = CreateMesh(go.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.enabled = false;

                return new MeshTarget
                {
                    Mesh = mesh,
                    Transform = go.transform,
                    Renderer = renderer
                };
            }

            private static int ValidStep(int step)
            {
                step = math.clamp(step, 1, MapGenerator.mapChunkSize);
                return math.min(math.ceilpow2(step), MapGenerator.mapChunkSize);
            }

            private static MeshLevel CreateLevel(int step, float skirtDepth)
            {
                int verts = BlockCells / step + 1;
                int vertexCount = TerrainIndexBuilder.VertexCount(verts);
                if (vertexCount > ushort.MaxValue + 1)
                    throw new System.InvalidOperationException($"Terrain level step {step}: {vertexCount} vertices exceed 16-bit indices");

                NativeArray<ushort> indices = TerrainIndexBuilder.Build(verts, Allocator.Persistent);

                return new MeshLevel
                {
                    Step = step,
                    Verts = verts,
                    VertexCount = vertexCount,
                    IndexCount = indices.Length,
                    SkirtDepth = skirtDepth * step * MapGenerator.scale,
                    Indices = indices
                };
            }

            private Mesh CreateMesh(string name)
            {
                var mesh = new Mesh { name = name, bounds = localBounds };
                mesh.MarkDynamic();
                return mesh;
            }

            public void UpdateChunk(long newCenter)
            {
                center = newCenter;
                hasCenter = true;

                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i].HasContent = false;
                    pool[i].Dirty = pool[i].Active;
                }

                ApplySector();

                if (!generator.TryBuildHeightWindow(center, out HeightWindow window))
                    return;

                Schedule(central, window, out Mesh.MeshDataArray data).Complete();
                Apply(central, data, MapGenerator.WindowOriginWorld(center));
            }

            public void SetSector(ViewSector newSector)
            {
                if (newSector == sector) return;
                sector = newSector;

                if (hasCenter) ApplySector();
            }

            private void ApplySector()
            {
                int count = pool.Length > 0 ? ViewWedge.Collect(sector, generator.LodRingCount, wanted) : 0;

                for (int i = 0; i < pool.Length; i++)
                {
                    MeshTarget slot = pool[i];
                    if (!slot.Active) continue;

                    if (IndexOfWanted(slot.Bx, slot.Bz, count) >= 0) continue;

                    if (slot.Dirty) slot.HasContent = false;
                    slot.Active = false;
                    slot.Dirty = false;
                    slot.Renderer.enabled = false;
                    slot.Stamp = ++stamp;
                }

                orderCount = 0;
                for (int w = 0; w < count; w++)
                {
                    ViewBlock block = wanted[w];
                    int slotIndex = FindActive(block.X, block.Z);

                    if (slotIndex < 0)
                    {
                        slotIndex = TakeSlot(block.X, block.Z, block.Ring);
                        if (slotIndex < 0) continue;

                        MeshTarget slot = pool[slotIndex];
                        bool reuse = slot.HasContent && slot.Bx == block.X && slot.Bz == block.Z && slot.Lod == block.Ring;

                        slot.Bx = block.X;
                        slot.Bz = block.Z;
                        slot.Lod = block.Ring;
                        slot.Level = levels[block.Ring];
                        slot.Active = true;
                        slot.Dirty = !reuse;
                        slot.HasContent = reuse;
                        slot.Transform.position = BlockPosition(slot);
                        slot.Renderer.enabled = reuse;
                    }

                    order[orderCount++] = slotIndex;
                }
            }

            private int IndexOfWanted(int bx, int bz, int count)
            {
                for (int w = 0; w < count; w++)
                    if (wanted[w].X == bx && wanted[w].Z == bz) return w;
                return -1;
            }

            private int FindActive(int bx, int bz)
            {
                for (int i = 0; i < pool.Length; i++)
                    if (pool[i].Active && pool[i].Bx == bx && pool[i].Bz == bz) return i;
                return -1;
            }

            private int TakeSlot(int bx, int bz, int lod)
            {
                int best = -1;
                for (int i = 0; i < pool.Length; i++)
                {
                    MeshTarget slot = pool[i];
                    if (slot.Active) continue;

                    if (slot.HasContent && slot.Bx == bx && slot.Bz == bz && slot.Lod == lod) return i;
                    if (best < 0 || slot.Stamp < pool[best].Stamp) best = i;
                }
                return best;
            }

            public void OnTerrainChanged(long chunkKey)
            {
                if (!hasCenter) return;

                if (MapGenerator.IsInWindow(center, chunkKey, MapGenerator.chunkScale + HeightWindow.Radius))
                    centralDirty = true;

                for (int i = 0; i < pool.Length; i++)
                {
                    MeshTarget slot = pool[i];
                    if (!slot.HasContent && !slot.Dirty) continue;
                    if (!MapGenerator.IsInWindow(BlockCenter(slot), chunkKey, MapGenerator.chunkScale + HeightWindow.Radius)) continue;

                    if (slot.Active) slot.Dirty = true;
                    else slot.HasContent = false;
                }
            }

            public void Tick(long deadline)
            {
                if (!hasCenter) return;

                if (centralDirty)
                {
                    centralDirty = false;
                    if (generator.TryBuildHeightWindow(center, out HeightWindow centralWindow))
                    {
                        Schedule(central, centralWindow, out Mesh.MeshDataArray centralData).Complete();
                        Apply(central, centralData, MapGenerator.WindowOriginWorld(center));
                    }
                }

                int n = 0;
                for (int k = 0; k < orderCount; k++)
                {
                    int index = order[k];
                    MeshTarget block = pool[index];
                    if (!block.Active || !block.Dirty) continue;
                    if (FrameBudget.Expired(deadline)) break;

                    long blockCenter = BlockCenter(block);
                    if (!generator.EnsureRegion(blockCenter, HeightWindow.Radius, block.Lod, deadline)) break;
                    if (!generator.TryBuildHeightWindow(blockCenter, out HeightWindow window)) break;

                    handles[n] = Schedule(block, window, out pendingData[n]);
                    pendingBlocks[n++] = index;
                    block.Dirty = false;
                }

                if (n == 0) return;

                JobHandle.CombineDependencies(handles.GetSubArray(0, n)).Complete();

                Vector3 origin = MapGenerator.WindowOriginWorld(center);
                for (int k = 0; k < n; k++)
                {
                    MeshTarget block = pool[pendingBlocks[k]];
                    Apply(block, pendingData[k], origin + BlockOffset(block));
                    pendingData[k] = default;
                    block.HasContent = true;
                    block.Renderer.enabled = true;
                    BuiltBlocks++;
                }
            }

            public void OnChunkDirty(long chunkKey) { }

            private long BlockCenter(MeshTarget block) =>
                MapGenerator.BlockCenterChunk(center, block.Bx, block.Bz);

            private static Vector3 BlockOffset(MeshTarget block) =>
                new(block.Bx * BlockWorld, 0f, block.Bz * BlockWorld);

            private Vector3 BlockPosition(MeshTarget block) =>
                MapGenerator.WindowOriginWorld(center) + BlockOffset(block);

            private JobHandle Schedule(MeshTarget target, in HeightWindow window, out Mesh.MeshDataArray data)
            {
                MeshLevel level = target.Level;

                data = Mesh.AllocateWritableMeshData(1);
                Mesh.MeshData meshData = data[0];

                meshData.SetVertexBufferParams(level.VertexCount, TerrainVertexLayout.Attributes);
                meshData.SetIndexBufferParams(level.IndexCount, IndexFormat.UInt16);
                meshData.GetIndexData<ushort>().CopyFrom(level.Indices);
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, level.IndexCount)
                {
                    bounds = localBounds,
                    firstVertex = 0,
                    vertexCount = level.VertexCount
                }, UpdateFlags);

                JobHandle handle = new TerrainVertexJob
                {
                    Heights = window,
                    Step = level.Step,
                    Verts = level.Verts,
                    CellSize = MapGenerator.scale,
                    SkirtDepth = level.SkirtDepth,
                    Vertices = meshData.GetVertexData<TerrainVertex>()
                }.ScheduleParallel(level.Verts + 4, RowBatch, default);

                JobHandle.ScheduleBatchedJobs();
                return handle;
            }

            private void Apply(MeshTarget target, Mesh.MeshDataArray data, Vector3 position)
            {
                Mesh.ApplyAndDisposeWritableMeshData(data, target.Mesh, UpdateFlags);
                target.Mesh.bounds = localBounds;
                target.Transform.position = position;
            }

            public void Dispose()
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    if (levels[i] != null && levels[i].Indices.IsCreated)
                        levels[i].Indices.Dispose();
                }

                if (handles.IsCreated) handles.Dispose();
            }
        }
    }
}
