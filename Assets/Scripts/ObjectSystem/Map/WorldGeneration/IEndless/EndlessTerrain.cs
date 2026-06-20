using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessTerrain : IEndless
        {
            public class LodLevel
            {
                public int Step;
                public int VertsPerAxis;
                public int QuadsPerAxis;
                
                public Mesh[] Meshes;
                public Transform[] Transforms;
                public Vector3[][] Vertices;
                public int[] Triangles;
                
                public (int dx, int dz)[] Offsets; 
            }
            private const int chunkScale = MapGenerator.chunkScale;
            private const int chunkCount = 2 * chunkScale + 1;
            private const int mapChunkSize = MapGenerator.mapChunkSize;
            private const int scale = MapGenerator.scale;
            private const int chunkSize = MapGenerator.chunkSize;

            private readonly MapGenerator generator;

            private readonly Mesh terrainMesh;
            private readonly int totalVerticesZ;
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            private readonly List<LodLevel> lodLevels = new();

            public EndlessTerrain(MapGenerator _generator, ChunkGenerationSettings _settings)
            {
                generator = _generator;

                int totalVerticesX = chunkCount * mapChunkSize + 1;
                totalVerticesZ    = chunkCount * mapChunkSize + 1;
                int totalVertices  = totalVerticesX * totalVerticesZ;
                int totalTris      = 6 * chunkCount * chunkCount * mapChunkSize * mapChunkSize;

                vertices  = new Vector3[totalVertices];
                triangles = new int[totalTris];
                GenerateTriangles();

                terrainMesh = new Mesh { vertices = vertices, triangles = triangles };
                terrainMesh.RecalculateNormals();
                terrainMesh.RecalculateTangents();
                terrainMesh.bounds = new Bounds(
                    new Vector3(chunkSize * chunkScale, 0, chunkSize * chunkScale),
                    new Vector3(chunkSize * chunkCount, 1000f, chunkSize * chunkCount));
                generator.terrainFilter.mesh = terrainMesh;

                int resolution = 1;

                var lodConfigs = new (int step, int ringSize)[]
                {
                    (4 / resolution, 1),
                    (8 / resolution, 2),
                    (16 / resolution, 3),
                    (32 / resolution, 4)
                };

                for (int i = 0; i < lodConfigs.Length; i++)
                {
                    var (step, ringSize) = lodConfigs[i];
                    lodLevels.Add(CreateLodLevel(step, ringSize));
                }
            }

            private LodLevel CreateLodLevel(int step, int ringIndex)
            {
                int quadsPerAxis = chunkCount * mapChunkSize / step;
                int vertsPerAxis = quadsPerAxis + 1;
                
                var offsets = new List<(int, int)>();
                
                for (int x = -ringIndex; x <= ringIndex; x++)
                {
                    for (int z = -ringIndex; z <= ringIndex; z++)
                    {
                        if (Mathf.Abs(x) != 1)
                            if(Mathf.Abs(z) < Mathf.Abs(x))
                                continue;

                        if (Mathf.Abs(x) == ringIndex || Mathf.Abs(z) == ringIndex)
                            offsets.Add((x, z));
                    }
                }

                var lod = new LodLevel
                {
                    Step = step,
                    VertsPerAxis = vertsPerAxis,
                    QuadsPerAxis = quadsPerAxis,
                    Offsets = offsets.ToArray(),
                    Triangles = BuildTrianglesForLod(quadsPerAxis, vertsPerAxis),
                    Vertices = new Vector3[offsets.Count][],
                    Meshes = new Mesh[offsets.Count],
                    Transforms = new Transform[offsets.Count]
                };

                var mat  = generator.terrainFilter.GetComponent<MeshRenderer>().sharedMaterial;
                var root = generator.terrainTransform.parent;

                for (int i = 0; i < offsets.Count; i++)
                {
                    lod.Vertices[i] = new Vector3[vertsPerAxis * vertsPerAxis];
                    
                    var go = new GameObject($"TerrainLOD_Step{step}_Ring{ringIndex}_{i}");
                    if (root != null) go.transform.SetParent(root, false);
                    lod.Transforms[i] = go.transform;

                    var mesh = new Mesh
                    {
                        vertices = lod.Vertices[i],
                        triangles = lod.Triangles,
                        bounds = new Bounds(
                            new Vector3(chunkSize * chunkScale, 0, chunkSize * chunkScale),
                            new Vector3(chunkSize * chunkCount, 1000f, chunkSize * chunkCount))
                    };

                    go.AddComponent<MeshFilter>().mesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    lod.Meshes[i] = mesh;
                }

                return lod;
            }

            private static int[] BuildTrianglesForLod(int quadsPerAxis, int vertsPerAxis)
            {
                var triangleIndex = new int[6 * quadsPerAxis * quadsPerAxis];
                int idx = triangleIndex.Length - 1;

                for (int x = 0; x < quadsPerAxis; x++)
                {
                    for (int z = 0; z < quadsPerAxis; z++)
                    {
                        int bl = x * vertsPerAxis + z;
                        int br = (x + 1) * vertsPerAxis + z;
                        int tr = (x + 1) * vertsPerAxis + z + 1;
                        int tl = x * vertsPerAxis + z + 1;

                        triangleIndex[idx]     = bl; 
                        triangleIndex[idx - 1] = br; 
                        triangleIndex[idx - 2] = tr;
                        
                        triangleIndex[idx - 3] = bl; 
                        triangleIndex[idx - 4] = tr; 
                        triangleIndex[idx - 5] = tl;
                        
                        idx -= 6;
                    }
                }
                return triangleIndex;
            }

            public async UniTaskVoid UpdateChunk(long position)
            {
                UpdateAllVertices(position);
                terrainMesh.vertices = vertices;
                terrainMesh.RecalculateNormals();

                var centralWorldPos = new Vector3(
                    (Position2Int.GetX(position) - 1) * chunkSize, 0,
                    (Position2Int.GetY(position) - 2) * chunkSize);
                    
                generator.terrainTransform.position = centralWorldPos;

                foreach (var lod in lodLevels)
                {
                    for (int i = 0; i < lod.Meshes.Length; i++)
                    {
                        var (dx, dz) = lod.Offsets[i];
                        long lodCentre = Position2Int.Offset(position, dx * chunkCount, dz * chunkCount);
                        
                        FillLodVertices(lodCentre, lod.Vertices[i], lod.Step, lod.VertsPerAxis);

                        lod.Meshes[i].vertices = lod.Vertices[i];
                        lod.Meshes[i].RecalculateNormals();

                        lod.Transforms[i].position = centralWorldPos +
                            new Vector3(dx * chunkCount * chunkSize, 0, dz * chunkCount * chunkSize);
                    }
                }
            }

            private void FillLodVertices(long centre, Vector3[] targetVertices, int lodStep, int vertsPerAxis)
            {
                int maxOrig = chunkCount * mapChunkSize;

                for (int vx = 0; vx < vertsPerAxis; vx++)
                {
                    for (int vz = 0; vz < vertsPerAxis; vz++)
                    {
                        int origX = vx * lodStep;
                        int origZ = vz * lodStep;

                        int chunkX = origX < maxOrig ? origX / mapChunkSize : chunkCount;
                        int chunkZ = origZ < maxOrig ? origZ / mapChunkSize : chunkCount;
                        int localX = origX < maxOrig ? origX % mapChunkSize : 0;
                        int localZ = origZ < maxOrig ? origZ % mapChunkSize : 0;

                        long chunkPos = Position2Int.Offset(centre, chunkX - chunkScale, chunkZ - chunkScale - 1);
                        float h = generator.GetMapData(chunkPos).HeightRaw[localZ * mapChunkSize + localX];

                        targetVertices[vx * vertsPerAxis + vz] = new Vector3(origX * scale, h, origZ * scale);
                    }
                }
            }

            private void GenerateTriangles()
            {
                int triangleIndex = triangles.Length - 1;

                for (int chunkX = 0; chunkX < chunkCount; chunkX++)
                {
                    for (int chunkY = 0; chunkY < chunkCount; chunkY++)
                    {
                        int vertexOffsetX = chunkX * mapChunkSize;
                        int vertexOffsetZ = chunkY * mapChunkSize;

                        for (int x = 0; x < mapChunkSize; x++)
                        {
                            for (int y = 0; y < mapChunkSize; y++)
                            {
                                int bl = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y;
                                int br = (vertexOffsetX + x + 1) * totalVerticesZ + vertexOffsetZ + y;
                                int tr = (vertexOffsetX + x + 1) * totalVerticesZ + vertexOffsetZ + y + 1;
                                int tl = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y + 1;

                                triangles[triangleIndex]     = bl;
                                triangles[triangleIndex - 1] = br;
                                triangles[triangleIndex - 2] = tr;
                                
                                triangles[triangleIndex - 3] = bl;
                                triangles[triangleIndex - 4] = tr;
                                triangles[triangleIndex - 5] = tl;
                                
                                triangleIndex -= 6;
                            }
                        }
                    }
                }
            }

            private void UpdateAllVertices(long centre)
            {
                for (int chunkX = 0; chunkX < chunkCount; chunkX++)
                {
                    for (int chunkY = -1; chunkY < chunkCount - 1; chunkY++)
                    {
                        UpdateChunkVertices(
                            Position2Int.Offset(centre, chunkX - chunkScale, chunkY - chunkScale),
                            chunkX, chunkY + 1);
                    }
                }
            }

            private void UpdateChunkVertices(long chunkPos, int gridX, int gridY)
            {
                int vertexOffsetX = gridX * mapChunkSize;
                int vertexOffsetZ = gridY * mapChunkSize;

                ChunkData chunkData = generator.GetMapData(chunkPos);

                for (int x = 0; x < mapChunkSize; x++)
                {
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        int vertexIndex = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y;
                        float h = chunkData.HeightRaw[y * mapChunkSize + x];
                        vertices[vertexIndex] = new Vector3((vertexOffsetX + x) * scale, h, (vertexOffsetZ + y) * scale);
                    }
                }

                if (gridX == chunkCount - 1)
                {
                    ChunkData nearData = generator.GetMapData(Position2Int.Offset(chunkPos, 1, 0));
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        int vi = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + y;
                        vertices[vi] = new Vector3((vertexOffsetX + mapChunkSize) * scale,
                            nearData.HeightRaw[y * mapChunkSize], (vertexOffsetZ + y) * scale);
                    }
                }

                if (gridY == chunkCount - 1)
                {
                    ChunkData nearData = generator.GetMapData(Position2Int.Offset(chunkPos, 0, 1));
                    for (int x = 0; x < mapChunkSize; x++)
                    {
                        int vi = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + mapChunkSize;
                        vertices[vi] = new Vector3((vertexOffsetX + x) * scale,
                            nearData.HeightRaw[x], (vertexOffsetZ + mapChunkSize) * scale);
                    }
                }

                if (gridX == chunkCount - 1 && gridY == chunkCount - 1)
                {
                    ChunkData nearData = generator.GetMapData(Position2Int.Offset(chunkPos, 1, 1));
                    int vi = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + mapChunkSize;
                    vertices[vi] = new Vector3((vertexOffsetX + mapChunkSize) * scale,
                        nearData.HeightRaw[0], (vertexOffsetZ + mapChunkSize) * scale);
                }
            }
        }

    }
}
