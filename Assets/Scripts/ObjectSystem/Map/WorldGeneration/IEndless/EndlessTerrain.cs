using UnityEngine;
using Cysharp.Threading.Tasks;
using TheRavine.Extensions;

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

            public EndlessTerrain(MapGenerator _generator, ChunkGenerationSettings _settings)
            {
                generator = _generator;
                
                totalVerticesX = chunkCount * mapChunkSize + 1;
                totalVerticesZ = chunkCount * mapChunkSize + 1;
                totalVertices = totalVerticesX * totalVerticesZ;
                totalTriangles = 6 * chunkCount * chunkCount * mapChunkSize * mapChunkSize;
                
                vertices = new Vector3[totalVertices];
                triangles = new int[totalTriangles];
                
                
                GenerateTriangles();
                
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
                int triangleIndex = totalTriangles - 1;
                
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
                                triangles[triangleIndex - 1] = bottomRight;
                                triangles[triangleIndex - 2] = topRight;
                                
                                triangles[triangleIndex - 3] = bottomLeft;
                                triangles[triangleIndex - 4] = topRight;
                                triangles[triangleIndex - 5] = topLeft;
                                
                                triangleIndex -= 6;
                            }
                        }
                    }
                }
            }

            public async UniTaskVoid UpdateChunk(long position)
            {
                UpdateAllVertices(position);
                
                terrainMesh.vertices = vertices;
                terrainMesh.RecalculateNormals();
                
                generator.terrainTransform.position = new Vector3(
                    (Position2Int.GetX(position) - 1) * generationSize, 0,
                    (Position2Int.GetY(position) - 2) * generationSize);

                await UniTask.CompletedTask;
            }

            private void UpdateAllVertices(long centre)
            {
                for (int chunkX = 0; chunkX < chunkCount; chunkX++)
                {
                    for (int chunkY = -1; chunkY < chunkCount - 1; chunkY++)
                    {

                        UpdateChunkVertices(Position2Int.Offset(centre, chunkX - chunkScale, chunkY - chunkScale), chunkX, chunkY + 1);
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
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + x) * scale, 
                            h, 
                            (vertexOffsetZ + y) * scale
                        );
                    }
                }
                
                if (gridX == chunkCount - 1)
                {
                    long chunkNearPos = Position2Int.Offset(chunkPos, 1, 0);
                    chunkData = generator.GetMapData(chunkNearPos);
                    
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        int vertexIndex = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + y;

                        float h = chunkData.HeightRaw[y * mapChunkSize];
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + mapChunkSize) * scale, 
                            h,
                            (vertexOffsetZ + y) * scale
                        );
                    }
                }
            
                if (gridY == chunkCount - 1)
                {
                    long chunkNearPos = Position2Int.Offset(chunkPos, 0, 1);
                    chunkData = generator.GetMapData(chunkNearPos);
                    
                    for (int x = 0; x < mapChunkSize; x++)
                    {
                        int vertexIndex = (vertexOffsetX + x) * totalVerticesZ + vertexOffsetZ + mapChunkSize;

                        float h = chunkData.HeightRaw[x];
                        vertices[vertexIndex] = new Vector3(
                            (vertexOffsetX + x) * scale, 
                            h, 
                            (vertexOffsetZ + mapChunkSize) * scale
                        );
                    }
                }
                
                if (gridX == chunkCount - 1 && gridY == chunkCount - 1)
                {
                    long chunkNearPos = Position2Int.Offset(chunkPos, 1, 1);
                    chunkData = generator.GetMapData(chunkNearPos);

                    float h = chunkData.HeightRaw[0];
                    int vertexIndex = (vertexOffsetX + mapChunkSize) * totalVerticesZ + vertexOffsetZ + mapChunkSize;
                    vertices[vertexIndex] = new Vector3(
                        (vertexOffsetX + mapChunkSize) * scale, 
                        h, 
                        (vertexOffsetZ + mapChunkSize) * scale
                    );
                }
            }
        }

    }
}
