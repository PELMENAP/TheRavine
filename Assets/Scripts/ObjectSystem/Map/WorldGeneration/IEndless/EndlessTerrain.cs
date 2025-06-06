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

                ushort trianglCount = 0;
                int[] triangles = new int[6 * mapChunkSize * mapChunkSize];
                for (int x = 0; x < mapChunkSize; x++)
                {
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        triangles[trianglCount] = x * (mapChunkSize + 1) + y;
                        triangles[trianglCount + 1] = (x + 1) * (mapChunkSize + 1) + y;
                        triangles[trianglCount + 2] = (x + 1) * (mapChunkSize + 1) + y + 1;
                        triangles[trianglCount + 3] = x * (mapChunkSize + 1) + y;
                        triangles[trianglCount + 4] = (x + 1) * (mapChunkSize + 1) + y + 1;
                        triangles[trianglCount + 5] = x * (mapChunkSize + 1) + y + 1;
                        trianglCount += 6;
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
            public void UpdateChunk(Vector2Int Vposition)
            {
                int count = 0;
                for (int yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    for (int xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                    {
                        CreateComplexMesh(new Vector2Int(Vposition.x + yOffset, Vposition.y + xOffset), combine[count].mesh);
                        combine[count].transform = Matrix4x4.TRS(new Vector3(yOffset * generationSize, xOffset * generationSize, 0), Quaternion.identity, Vector3.one);
                        count++;
                    }
                combineMesh.CombineMeshes(combine);
                generator.terrainF.mesh = combineMesh;
                generator.terrainT.position = (Vector2)Vposition * generationSize;
            }
            private readonly Vector3[] vertices = new Vector3[(mapChunkSize + 1) * (mapChunkSize + 1)];
            private readonly Vector2Int up = new Vector2Int(0, 1), right = new Vector2Int(1, 0), diag = new Vector2Int(1, 1);
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
