using System.Collections.Generic;
using System.Runtime.InteropServices;
using TheRavine.Extensions;
using TheRavine.ViewControl;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public sealed class EndlessLiquids : IEndless, IViewDirectional
        {
            private const float BlockWorld = MapGenerator.WindowSide * MapGenerator.chunkSize;
            private const float BoundsHeight = 64f;
            private const int MinCells = 8;
            private const int MaxCells = 128;
            private const int NegX = 1, PosX = 2, NegZ = 4, PosZ = 8;
            private const MeshUpdateFlags UpdateFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            [StructLayout(LayoutKind.Sequential)]
            private struct WaterVertex
            {
                public float3 Position;
                public float3 Normal;
                public float4 Tangent;
            }

            private static readonly VertexAttributeDescriptor[] Attributes =
            {
                new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            };

            private sealed class WaterBlock
            {
                public Transform Transform;
                public MeshFilter Filter;
                public MeshRenderer Renderer;
                public GameObject Owned;
                public int Bx;
                public int Bz;
                public int Level;
            }

            private readonly MapGenerator generator;
            private readonly int ringCount;
            private readonly int baseCells;
            private readonly Bounds localBounds;
            private readonly WaterBlock central;
            private readonly WaterBlock[] pool;
            private readonly ViewBlock[] wanted;
            private readonly Dictionary<int, Mesh> meshes = new();
            private readonly Mesh centralOriginal;

            private long center;
            private bool hasCenter;
            private ViewSector sector;

            public EndlessLiquids(MapGenerator _generator, ChunkGenerationSettings settings)
            {
                generator = _generator;
                sector = generator.Sector;
                ringCount = generator.LodRingCount;
                baseCells = math.clamp(math.ceilpow2(settings.waterCells), MinCells, MaxCells);
                float reach = (ringCount * MapGenerator.WindowSide + 2) * MapGenerator.chunkSize * 1.4143f;
                float boundsHeight = BoundsHeight + 2f * CurvedWorld.MaxDrop(reach);
                localBounds = new Bounds(
                    new Vector3(BlockWorld * 0.5f, 0f, BlockWorld * 0.5f),
                    new Vector3(BlockWorld, boundsHeight, BlockWorld));

                Transform central0 = generator.waterTransform;
                central0.localScale = Vector3.one;

                var centralFilter = central0.GetComponent<MeshFilter>();
                if (centralFilter == null) centralFilter = central0.gameObject.AddComponent<MeshFilter>();
                centralOriginal = centralFilter.sharedMesh;

                var renderer = central0.GetComponent<MeshRenderer>();
                Material material = renderer.sharedMaterial;
                Transform root = central0.parent;

                int poolSize = ringCount > 0 ? ViewWedge.MaxBlocks(ringCount) : 0;
                pool = new WaterBlock[poolSize];
                wanted = new ViewBlock[math.max(poolSize, 1)];

                central = new WaterBlock { Transform = central0, Filter = centralFilter, Renderer = renderer, Bx = 0, Bz = 0, Level = 0 };
                for (int i = 0; i < poolSize; i++)
                    pool[i] = CreateBlock(i, material, root);

                ApplySector();
            }

            private WaterBlock CreateBlock(int index, Material material, Transform root)
            {
                var go = new GameObject($"WaterBlock_{index}") { layer = generator.waterTransform.gameObject.layer };
                if (root != null) go.transform.SetParent(root, false);

                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.enabled = false;
                var source = generator.waterTransform.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.lightProbeUsage = source.lightProbeUsage;
                renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                renderer.motionVectorGenerationMode = source.motionVectorGenerationMode;

                return new WaterBlock
                {
                    Transform = go.transform,
                    Filter = filter,
                    Renderer = renderer,
                    Owned = go,
                    Level = -1
                };
            }

            private int LevelAt(int bx, int bz)
            {
                int ring = ViewWedge.Ring(bx, bz);
                if (ring > ringCount) return -1;
                return ViewWedge.Contains(sector, bx, bz) ? ring : -1;
            }

            private int CellsOf(int level) => math.max(MinCells, baseCells >> level);

            private int StitchMask(int bx, int bz, int level)
            {
                int cells = CellsOf(level);
                int mask = 0;
                if (Coarser(bx - 1, bz, cells)) mask |= NegX;
                if (Coarser(bx + 1, bz, cells)) mask |= PosX;
                if (Coarser(bx, bz - 1, cells)) mask |= NegZ;
                if (Coarser(bx, bz + 1, cells)) mask |= PosZ;
                return mask;
            }

            private bool Coarser(int bx, int bz, int cells)
            {
                int level = LevelAt(bx, bz);
                return level >= 0 && CellsOf(level) < cells;
            }

            private void AssignMesh(WaterBlock block)
            {
                int key = (block.Level << 4) | StitchMask(block.Bx, block.Bz, block.Level);
                if (!meshes.TryGetValue(key, out Mesh mesh))
                {
                    mesh = BuildMesh(block.Level, key & 15);
                    meshes.Add(key, mesh);
                }
                block.Filter.sharedMesh = mesh;
            }

            private void ApplySector()
            {
                AssignMesh(central);

                int count = pool.Length > 0 ? ViewWedge.Collect(sector, ringCount, wanted) : 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    WaterBlock block = pool[i];
                    if (i >= count)
                    {
                        block.Renderer.enabled = false;
                        block.Level = -1;
                        continue;
                    }

                    block.Bx = wanted[i].X;
                    block.Bz = wanted[i].Z;
                    block.Level = wanted[i].Ring;
                    AssignMesh(block);
                    block.Renderer.enabled = true;
                }

                if (hasCenter) Place();
            }

            private Mesh BuildMesh(int level, int mask)
            {
                int cells = CellsOf(level);
                int verts = cells + 1;
                float cellSize = BlockWorld / cells;

                var vertices = new NativeArray<WaterVertex>(verts * verts, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int z = 0; z < verts; z++)
                {
                    for (int x = 0; x < verts; x++)
                    {
                        vertices[z * verts + x] = new WaterVertex
                        {
                            Position = new float3(x * cellSize, 0f, z * cellSize),
                            Normal = new float3(0f, 1f, 0f),
                            Tangent = new float4(1f, 0f, 0f, 1f)
                        };
                    }
                }

                var indices = new NativeList<ushort>(cells * cells * 6, Allocator.Temp);
                for (int z = 0; z < cells; z++)
                {
                    for (int x = 0; x < cells; x++)
                    {
                        ushort a = Vertex(x, z, cells, verts, mask);
                        ushort b = Vertex(x + 1, z, cells, verts, mask);
                        ushort c = Vertex(x, z + 1, cells, verts, mask);
                        ushort d = Vertex(x + 1, z + 1, cells, verts, mask);

                        if (a != c && c != d && a != d)
                        {
                            indices.Add(a);
                            indices.Add(c);
                            indices.Add(d);
                        }

                        if (a != d && d != b && a != b)
                        {
                            indices.Add(a);
                            indices.Add(d);
                            indices.Add(b);
                        }
                    }
                }

                var mesh = new Mesh { name = $"Water_L{level}_M{mask}", bounds = localBounds };
                mesh.SetVertexBufferParams(vertices.Length, Attributes);
                mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, UpdateFlags);
                mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                mesh.SetIndexBufferData(indices.AsArray(), 0, 0, indices.Length, UpdateFlags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length)
                {
                    bounds = localBounds,
                    firstVertex = 0,
                    vertexCount = vertices.Length
                }, UpdateFlags);
                mesh.bounds = localBounds;

                vertices.Dispose();
                indices.Dispose();
                return mesh;
            }

            private static ushort Vertex(int x, int z, int cells, int verts, int mask)
            {
                if ((x == 0 && (mask & NegX) != 0) || (x == cells && (mask & PosX) != 0))
                    z &= ~1;
                if ((z == 0 && (mask & NegZ) != 0) || (z == cells && (mask & PosZ) != 0))
                    x &= ~1;
                return (ushort)(z * verts + x);
            }

            public void UpdateChunk(long newCenter)
            {
                center = newCenter;
                hasCenter = true;

                ApplySector();

                float waterXPosition = (Position2Int.GetX(center) + 0.5f) * MapGenerator.chunkSize;
                float waterZPosition = (Position2Int.GetY(center) + 0.5f) * MapGenerator.chunkSize;
                RippleStampSystem.Instance.SetWaterPosition(waterXPosition, waterZPosition);
            }

            public void SetSector(ViewSector newSector)
            {
                if (newSector == sector) return;
                sector = newSector;

                ApplySector();
            }

            private void Place()
            {
                Vector3 origin = MapGenerator.WindowOriginWorld(center);
                origin.y = generator.waterOffset.y;

                central.Transform.position = origin;
                for (int i = 0; i < pool.Length; i++)
                {
                    WaterBlock block = pool[i];
                    if (block.Level < 0) continue;
                    block.Transform.position = origin + new Vector3(block.Bx * BlockWorld, 0f, block.Bz * BlockWorld);
                }
            }

            public void Tick(long deadline) { }

            public void OnChunkDirty(long chunkKey) { }

            public void Dispose()
            {
                if (pool != null)
                {
                    for (int i = 0; i < pool.Length; i++)
                    {
                        if (pool[i].Owned != null)
                            Object.Destroy(pool[i].Owned);
                    }
                }

                if (central != null && central.Filter != null)
                    central.Filter.sharedMesh = centralOriginal;

                foreach (var pair in meshes)
                {
                    if (pair.Value != null)
                        Object.Destroy(pair.Value);
                }
                meshes.Clear();
            }
        }
    }
}
