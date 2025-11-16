using UnityEngine;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessTerrain : IEndless
        {
            private const int chunkScale = MapGenerator.chunkScale, chunkCount = 2 * chunkScale + 1, mapChunkSize = MapGenerator.mapChunkSize;
            private readonly MapGenerator generator;
            private readonly int scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
            private readonly Mesh combineMesh;
            public EndlessTerrain(MapGenerator _generator)
            {
                generator = _generator;
                combineMesh = new Mesh();

                ushort triangleCount = 0;
                int[] triangles = new int[6 * mapChunkSize * mapChunkSize];
                for (int x = 0; x < mapChunkSize; x++)
                {
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        triangles[triangleCount] = x * (mapChunkSize + 1) + y;
                        triangles[triangleCount + 1] = (x + 1) * (mapChunkSize + 1) + y;
                        triangles[triangleCount + 2] = (x + 1) * (mapChunkSize + 1) + y + 1;
                        triangles[triangleCount + 3] = x * (mapChunkSize + 1) + y;
                        triangles[triangleCount + 4] = (x + 1) * (mapChunkSize + 1) + y + 1;
                        triangles[triangleCount + 5] = x * (mapChunkSize + 1) + y + 1;
                        triangleCount += 6;
                    }
                }
                for (int i = 0; i < chunkCount * chunkCount; i++)
                {
                    combine[i].mesh = new Mesh
                    {
                        vertices = vertices,
                        triangles = triangles
                    };
                }
            }
            private readonly CombineInstance[] combine = new CombineInstance[chunkCount * chunkCount];
            private static Quaternion defRotation = Quaternion.Euler(-90, 0, 0);
            public void UpdateChunk(Vector2Int Position) 
            {
                int count = 0;
                for (int yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    for (int xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                    {
                        CreateComplexMesh(new Vector2Int(-Position.x + yOffset, Position.y + xOffset), combine[count].mesh);
                        combine[count].transform = Matrix4x4.TRS(new Vector3(yOffset * generationSize, -4, -xOffset * generationSize), defRotation, Vector3.one);
                        count++;
                    }
                combineMesh.CombineMeshes(combine);
                generator.terrainFilter.mesh = combineMesh;
                generator.terrainCollider.sharedMesh = combineMesh;
                generator.terrainTransform.position = new Vector3((Position.x + 1) * generationSize, 0 , (Position.y - 1) * generationSize);
            }
            private readonly Vector3[] vertices = new Vector3[(mapChunkSize + 1) * (mapChunkSize + 1)];
            private readonly Vector2Int up = new(0, 1), right = new(1, 0), diag = new(1, 1);
            private void CreateComplexMesh(Vector2Int centre, Mesh mesh)
            {
                int[,] heightMap = generator.GetMapData(centre).heightMap;
                for (int x = 0; x < mapChunkSize; x++)
                    for (int y = 0; y < mapChunkSize; y++)
                        vertices[x * (mapChunkSize + 1) + y] = new Vector3(x * scale, y * scale, heightMap[x, y]);

                heightMap = generator.GetMapData(centre + up).heightMap;
                for (int x = 0; x < mapChunkSize; x++)
                    vertices[x * (mapChunkSize + 1) + mapChunkSize] = new Vector3(x * scale, generationSize, heightMap[x, 0]);
                heightMap = generator.GetMapData(centre + right).heightMap;
                for (int y = 0; y < mapChunkSize; y++)
                    vertices[mapChunkSize * (mapChunkSize + 1) + y] = new Vector3(generationSize, y * scale, heightMap[0, y]);
                vertices[mapChunkSize * (mapChunkSize + 1) + mapChunkSize] = new Vector3(generationSize, generationSize, generator.GetMapData(centre + diag).heightMap[0, 0]);

                mesh.vertices = vertices;
            }
        }
    }
}
