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
                public GameObject Owned;
                public int Dx;
                public int Row;
                public int Level;
            }

            private readonly MapGenerator generator;
            private readonly int ringCount;
            private readonly int baseCells;
            private readonly Bounds localBounds;
            private readonly WaterBlock[] blocks;
            private readonly Dictionary<int, Mesh> meshes = new();
            private readonly Mesh centralOriginal;

            private long center;
            private bool hasCenter;
            private ViewFacing facing;

            public EndlessLiquids(MapGenerator _generator, ChunkGenerationSettings settings)
            {
                generator = _generator;
                facing = generator.Facing;
                ringCount = generator.LodRingCount;
                baseCells = math.clamp(math.ceilpow2(settings.waterCells), MinCells, MaxCells);
                localBounds = new Bounds(
                    new Vector3(BlockWorld * 0.5f, 0f, BlockWorld * 0.5f),
                    new Vector3(BlockWorld, BoundsHeight, BlockWorld));

                Transform central = generator.waterTransform;
                central.localScale = Vector3.one;

                var centralFilter = central.GetComponent<MeshFilter>();
                if (centralFilter == null) centralFilter = central.gameObject.AddComponent<MeshFilter>();
                centralOriginal = centralFilter.sharedMesh;

                var renderer = central.GetComponent<MeshRenderer>();
                Material material = renderer.sharedMaterial;
                Transform root = central.parent;

                int count = 3;
                for (int r = 1; r <= ringCount; r++)
                    count += 2 * r + 1;

                blocks = new WaterBlock[count];
                blocks[0] = new WaterBlock { Transform = central, Filter = centralFilter, Dx = 0, Row = 0, Level = 0 };

                int b = 1;
                for (int side = -1; side <= 1; side += 2)
                    blocks[b++] = CreateBlock(side, 0, material, root);

                for (int row = 1; row <= ringCount; row++)
                {
                    for (int k = 0; k <= 2 * row; k++)
                    {
                        int dx = ((k + 1) >> 1) * ((k & 1) == 1 ? -1 : 1);
                        blocks[b++] = CreateBlock(dx, row, material, root);
                    }
                }

                AssignMeshes();
            }

            private WaterBlock CreateBlock(int dx, int row, Material material, Transform root)
            {
                var go = new GameObject($"WaterLOD_Ring{row}_{dx}") { layer = generator.waterTransform.gameObject.layer };
                if (root != null) go.transform.SetParent(root, false);

                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
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
                    Owned = go,
                    Dx = dx,
                    Row = row,
                    Level = LevelAt(dx, row)
                };
            }

            private int LevelAt(int dx, int row)
            {
                if (row < 0 || row > ringCount) return -1;
                if (row == 0)
                {
                    int ax = math.abs(dx);
                    return ax == 0 ? 0 : (ax == 1 ? 1 : -1);
                }
                return math.abs(dx) <= row ? row : -1;
            }

            private int CellsOf(int level) => math.max(MinCells, baseCells >> level);

            private int StitchMask(WaterBlock block)
            {
                int cells = CellsOf(block.Level);
                int2 right = facing.Right();
                int2 forward = facing.Forward();
                int mask = 0;
                if (Coarser(block.Dx - right.x, block.Row - forward.x, cells)) mask |= NegX;
                if (Coarser(block.Dx + right.x, block.Row + forward.x, cells)) mask |= PosX;
                if (Coarser(block.Dx - right.y, block.Row - forward.y, cells)) mask |= NegZ;
                if (Coarser(block.Dx + right.y, block.Row + forward.y, cells)) mask |= PosZ;
                return mask;
            }

            private bool Coarser(int dx, int row, int cells)
            {
                int level = LevelAt(dx, row);
                return level >= 0 && CellsOf(level) < cells;
            }

            private void AssignMeshes()
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    WaterBlock block = blocks[i];
                    int key = (block.Level << 4) | StitchMask(block);
                    if (!meshes.TryGetValue(key, out Mesh mesh))
                    {
                        mesh = BuildMesh(block.Level, key & 15);
                        meshes.Add(key, mesh);
                    }
                    block.Filter.sharedMesh = mesh;
                }
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

                Place();

                float waterXPosition = (Position2Int.GetX(center) + 0.5f) * MapGenerator.chunkSize;
                float waterZPosition = (Position2Int.GetY(center) + 0.5f) * MapGenerator.chunkSize;
                RippleStampSystem.Instance.SetWaterPosition(waterXPosition, waterZPosition);
            }

            public void SetFacing(ViewFacing newFacing)
            {
                if (newFacing == facing) return;
                facing = newFacing;

                AssignMeshes();
                if (hasCenter) Place();
            }

            private void Place()
            {
                Vector3 origin = MapGenerator.WindowOriginWorld(center);
                origin.y = generator.waterOffset.y;
                int2 right = facing.Right();
                int2 forward = facing.Forward();

                for (int i = 0; i < blocks.Length; i++)
                {
                    WaterBlock block = blocks[i];
                    block.Transform.position = origin + new Vector3(
                        (right.x * block.Dx + forward.x * block.Row) * BlockWorld,
                        0f,
                        (right.y * block.Dx + forward.y * block.Row) * BlockWorld);
                }
            }

            public void Tick(long deadline) { }

            public void OnChunkDirty(long chunkKey) { }

            public void Dispose()
            {
                if (blocks != null)
                {
                    for (int i = 0; i < blocks.Length; i++)
                    {
                        if (blocks[i].Owned != null)
                            Object.Destroy(blocks[i].Owned);
                    }

                    if (blocks[0].Filter != null)
                        blocks[0].Filter.sharedMesh = centralOriginal;
                }

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
