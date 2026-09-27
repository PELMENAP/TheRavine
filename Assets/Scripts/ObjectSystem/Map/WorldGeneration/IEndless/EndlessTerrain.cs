using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Extensions;

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
                public int Ring;
                public int Dx;
                public bool Dirty;
            }

            private readonly MapGenerator generator;
            private readonly MeshLevel[] levels;
            private readonly MeshTarget central;
            private readonly MeshTarget[] blocks;
            private readonly Bounds localBounds;
            private readonly Mesh.MeshDataArray[] pendingData;
            private readonly int[] pendingBlocks;
            private NativeArray<JobHandle> handles;

            private long center;
            private bool hasCenter;
            private int facing;

            public EndlessTerrain(MapGenerator _generator, ChunkGenerationSettings settings)
            {
                generator = _generator;
                facing = generator.Facing;
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
                    Transform = generator.terrainTransform
                };
                generator.terrainFilter.sharedMesh = central.Mesh;

                int blockCount = 0;
                for (int r = 1; r <= ringCount; r++)
                    blockCount += 2 * r + 1;

                blocks = new MeshTarget[blockCount];
                pendingData = new Mesh.MeshDataArray[math.max(blockCount, 1)];
                pendingBlocks = new int[math.max(blockCount, 1)];
                handles = new NativeArray<JobHandle>(math.max(blockCount, 1), Allocator.Persistent);

                var material = generator.terrainFilter.GetComponent<MeshRenderer>().sharedMaterial;
                var root = generator.terrainTransform.parent;

                int b = 0;
                for (int ring = 1; ring <= ringCount; ring++)
                {
                    for (int k = 0; k <= 2 * ring; k++)
                    {
                        int dx = ((k + 1) >> 1) * ((k & 1) == 1 ? -1 : 1);

                        var go = new GameObject($"TerrainLOD_Ring{ring}_{dx}");
                        if (root != null) go.transform.SetParent(root, false);

                        Mesh mesh = CreateMesh(go.name);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        go.AddComponent<MeshRenderer>().sharedMaterial = material;

                        blocks[b++] = new MeshTarget
                        {
                            Level = levels[ring],
                            Mesh = mesh,
                            Transform = go.transform,
                            Ring = ring,
                            Dx = dx,
                            Dirty = true
                        };
                    }
                }
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

                for (int i = 0; i < blocks.Length; i++)
                    blocks[i].Dirty = true;

                if (!generator.TryBuildHeightWindow(center, out HeightWindow window))
                    return;

                Schedule(central, window, out Mesh.MeshDataArray data).Complete();
                Apply(central, data, MapGenerator.WindowOriginWorld(center));
            }

            public void SetFacing(int newFacing)
            {
                if (newFacing == facing) return;
                facing = newFacing;

                for (int i = 0; i < blocks.Length; i++)
                    blocks[i].Dirty = true;
            }

            public void Tick(long deadline)
            {
                if (!hasCenter) return;

                int n = 0;
                for (int i = 0; i < blocks.Length; i++)
                {
                    MeshTarget block = blocks[i];
                    if (!block.Dirty) continue;
                    if (FrameBudget.Expired(deadline)) break;

                    long blockCenter = BlockCenter(block);
                    if (!generator.EnsureRegion(blockCenter, HeightWindow.Radius, block.Ring, deadline)) break;
                    if (!generator.TryBuildHeightWindow(blockCenter, out HeightWindow window)) break;

                    handles[n] = Schedule(block, window, out pendingData[n]);
                    pendingBlocks[n++] = i;
                    block.Dirty = false;
                }

                if (n == 0) return;

                JobHandle.CombineDependencies(handles.GetSubArray(0, n)).Complete();

                Vector3 origin = MapGenerator.WindowOriginWorld(center);
                for (int k = 0; k < n; k++)
                {
                    MeshTarget block = blocks[pendingBlocks[k]];
                    Apply(block, pendingData[k], origin + BlockOffset(block));
                    pendingData[k] = default;
                }
            }

            public void OnChunkDirty(long chunkKey) { }

            private long BlockCenter(MeshTarget block) =>
                Position2Int.Offset(center,
                    block.Dx * MapGenerator.WindowSide,
                    block.Ring * facing * MapGenerator.WindowSide);

            private Vector3 BlockOffset(MeshTarget block) =>
                new(block.Dx * BlockWorld, 0f, block.Ring * facing * BlockWorld);

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
