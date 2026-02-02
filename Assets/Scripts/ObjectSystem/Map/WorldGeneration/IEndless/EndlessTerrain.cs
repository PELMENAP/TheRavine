using UnityEngine;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessTerrain : IEndless
        {
            private const int chunkScale = MapGenerator.chunkScale, chunkCount = 2 * chunkScale + 1, mapChunkSize = MapGenerator.mapChunkSize;
            private const int scale = MapGenerator.scale, generationSize = scale * mapChunkSize;
            
            private readonly Mesh terrainMesh;
            private readonly MapGenerator generator;
            
            private readonly int totalVerticesX;
            private readonly int totalVerticesZ;
            private readonly int totalVertices;
            private readonly int totalTriangles;
            
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            private readonly Vector2Int up = new(0, 1), right = new(1, 0), diag = new(1, 1);

            public EndlessTerrain(MapGenerator _generator)
            {
                generator = _generator;
                
                totalVerticesX = chunkCount * mapChunkSize + 1;
                totalVerticesZ = chunkCount * mapChunkSize + 1;
                totalVertices = totalVerticesX * totalVerticesZ;
                totalTriangles = 6 * chunkCount * chunkCount * mapChunkSize * mapChunkSize;
                
                vertices = new Vector3[totalVertices];
                triangles = new int[totalTriangles];
                
                
                GenerateTriangles();
                System.Array.Reverse(triangles);
                
                terrainMesh = new Mesh
                {
                    vertices = vertices,
                    triangles = triangles
                };
                
                terrainMesh.RecalculateNormals();
                terrainMesh.RecalculateTangents();

                terrainMesh.bounds = new Bounds(
                    new Vector3(generationSize * chunkScale, 0, generationSize * chunkScale),
                    new Vector3(generationSize * chunkCount, 1000f, generationSize * chunkCount)
                );
                
                generator.terrainFilter.mesh = terrainMesh;
            }

            private void GenerateTriangles()
            {
                int triangleIndex = 0;
                
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
                                int bottomLeft = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y;
                                int bottomRight = (vertexOffsetX + x + 1) * totalVerticesZ + vertexOffsetZ + y;
                                int topRight = (vertexOffsetX + x + 1) * totalVerticesZ + vertexOffsetZ + y + 1;
                                int topLeft = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y + 1;
                                
                                triangles[triangleIndex] = bottomLeft;
                                triangles[triangleIndex + 1] = bottomRight;
                                triangles[triangleIndex + 2] = topRight;
                                
                                triangles[triangleIndex + 3] = bottomLeft;
                                triangles[triangleIndex + 4] = topRight;
                                triangles[triangleIndex + 5] = topLeft;
                                
                                triangleIndex += 6;
                            }
                        }
                    }
                }
            }

            public void UpdateChunk(Vector2Int position) 
            {
                UpdateAllVertices(position);
                
                terrainMesh.vertices = vertices;
                terrainMesh.RecalculateNormals();
                terrainMesh.RecalculateTangents();

                generator.terrainCollider.sharedMesh = terrainMesh;
                
                generator.terrainTransform.position = new Vector3(
                    (position.x - 1) * generationSize, 
                    0, 
                    (position.y - 2) * generationSize
                );
            }

            private void UpdateAllVertices(Vector2Int centre)
            {
                for (int chunkX = 0; chunkX < chunkCount; chunkX++)
                {
                    for (int chunkY = 0; chunkY < chunkCount; chunkY++)
                    {
                        Vector2Int chunkWorldPos = new(
                            centre.x - chunkScale + chunkX,
                            centre.y - chunkScale + chunkY
                        );
                        
                        UpdateChunkVertices(chunkWorldPos, chunkX, chunkY);
                    }
                }
            }

            private void UpdateChunkVertices(Vector2Int chunkPos, int gridX, int gridZ)
            {
                int vertexOffsetX = gridX * mapChunkSize;
                int vertexOffsetZ = gridZ * mapChunkSize;
                
                ChunkData chunkData = generator.GetMapData(chunkPos);
                int[,] heightMap = chunkData.heightMap;

                for (int x = 0; x < mapChunkSize; x++)
                {
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        int vertexIndex = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + y;
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + x) * scale, 
                            heightMap[x, y], 
                            (vertexOffsetZ + y) * scale
                        );
                    }
                }
                
                if (gridX == chunkCount - 1)
                {
                    chunkData = generator.GetMapData(chunkPos + right);
                    heightMap = chunkData.heightMap;
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        int vertexIndex = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + y;
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + mapChunkSize) * scale, 
                            heightMap[0, y], 
                            (vertexOffsetZ + y) * scale
                        );
                    }
                }
            
                if (gridZ == chunkCount - 1)
                {
                    chunkData = generator.GetMapData(chunkPos + up);
                    heightMap = chunkData.heightMap;
                    for (int x = 0; x < mapChunkSize; x++)
                    {
                        int vertexIndex = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + mapChunkSize;
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + x) * scale, 
                            heightMap[x, 0], 
                            (vertexOffsetZ + mapChunkSize) * scale
                        );
                    }
                }
                
                if (gridX == chunkCount - 1 && gridZ == chunkCount - 1)
                {
                    chunkData = generator.GetMapData(chunkPos + diag);
                    heightMap = chunkData.heightMap;
                    int vertexIndex = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + mapChunkSize;
                    vertices[vertexIndex] = new Vector3(
                        (vertexOffsetX + mapChunkSize) * scale, 
                        heightMap[0, 0], 
                        (vertexOffsetZ + mapChunkSize) * scale
                    );
                }
            }
        }

    }
}
