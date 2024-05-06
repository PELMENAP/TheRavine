using UnityEngine;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessTerrain : IEndless
        {
            private const byte chunkScale = MapGenerator.chunkScale, chunkCount = 2 * chunkScale + 1, mapChunkSize = MapGenerator.mapChunkSize;
            private readonly MapGenerator generator;
            private readonly byte scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
            private readonly Mesh combineMesh;
            public EndlessTerrain(MapGenerator _generator)
            {
                generator = _generator;
                combineMesh = new Mesh();

                ushort trianglCount = 0;
                int[] triangles = new int[6 * mapChunkSize * mapChunkSize];
                for (byte x = 0; x < mapChunkSize; x++)
                {
                    for (byte y = 0; y < mapChunkSize; y++)
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
                for (byte i = 0; i < chunkCount * chunkCount; i++)
                {
                    combine[i].mesh = new Mesh
                    {
                        vertices = vertices,
                        triangles = triangles
                    };
                }
            }
            private readonly CombineInstance[] combine = new CombineInstance[chunkCount * chunkCount];
            public void UpdateChunk(Vector2 Vposition)
            {
                byte count = 0;
                for (sbyte yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                    for (sbyte xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                    {
                        CreateComplexMesh(new Vector2(Vposition.x + yOffset, Vposition.y + xOffset), combine[count].mesh);
                        combine[count].transform = Matrix4x4.TRS(new Vector3(yOffset * generationSize, xOffset * generationSize, 0), Quaternion.identity, Vector3.one);
                        count++;
                    }
                combineMesh.CombineMeshes(combine);
                generator.terrainF.mesh = combineMesh;
                generator.terrainT.position = Vposition * generationSize;
            }
            private readonly Vector3[] vertices = new Vector3[(mapChunkSize + 1) * (mapChunkSize + 1)];
            private void CreateComplexMesh(Vector2 centre, Mesh mesh)
            {
                byte[,] heightMap = generator.GetMapData(centre).heightMap;
                for (byte x = 0; x < mapChunkSize; x++)
                    for (byte y = 0; y < mapChunkSize; y++)
                        vertices[x * (mapChunkSize + 1) + y] = new Vector3(x * scale, y * scale, heightMap[x, y]);

                for (byte x = 0; x < mapChunkSize; x++)
                    vertices[x * (mapChunkSize + 1) + mapChunkSize] = new Vector3(x * scale, generationSize, generator.GetMapData(centre + new Vector2(0, 1)).heightMap[x, 0]);
                for (byte y = 0; y < mapChunkSize; y++)
                    vertices[mapChunkSize * (mapChunkSize + 1) + y] = new Vector3(generationSize, y * scale, generator.GetMapData(centre + new Vector2(1, 0)).heightMap[0, y]);
                vertices[mapChunkSize * (mapChunkSize + 1) + mapChunkSize] = new Vector3(generationSize, generationSize, generator.GetMapData(centre + new Vector2(1, 1)).heightMap[0, 0]);

                mesh.vertices = vertices;
            }
        }
    }
}
