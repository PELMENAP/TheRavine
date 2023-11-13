using System.Collections.Generic;
using UnityEngine;

public class EndlessLiquids : IEndless
{
    private MapGenerator generator;
    private const int chunkCount = MapGenerator.chunkCount, mapChunkSize = MapGenerator.mapChunkSize;
    private int scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
    public EndlessLiquids(MapGenerator _generator)
    {
        generator = _generator;
    }

    private int currentChunkCoordX, currentChunkCoordY, countOfQuads;
    private int[,] map;
    private bool[,] meshMap = new bool[mapChunkSize * chunkCount, mapChunkSize * chunkCount];
    public void UpdateChunk(Vector3 Vposition)
    {
        currentChunkCoordX = Mathf.RoundToInt(Vposition.x);
        currentChunkCoordY = Mathf.RoundToInt(Vposition.y);
        countOfQuads = 0;
        for (int xOffset = 0; xOffset < chunkCount; xOffset++)
        {
            for (int yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                map = generator.GetMapData(new Vector2(-(currentChunkCoordX + xOffset), currentChunkCoordY + yOffset)).heightMap;
                for (int x = 0; x < mapChunkSize; x++)
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        if (map[x, y] <= MapGenerator.waterLevel)
                        {
                            meshMap[(chunkCount - 1 - xOffset) * mapChunkSize + x, (chunkCount - 1 - yOffset) * mapChunkSize + y] = true;
                            countOfQuads++;
                        }
                        else
                        {
                            meshMap[(chunkCount - 1 - xOffset) * mapChunkSize + x, (chunkCount - 1 - yOffset) * mapChunkSize + y] = false;
                        }
                    }
            }
        }
        generator.waterF.mesh = GetQuadWaterMeshMap(meshMap, countOfQuads, mapChunkSize * chunkCount);
        generator.waterT.position = new Vector3(currentChunkCoordX, currentChunkCoordY) * generationSize + new Vector3(generationSize, generationSize) / 2 * 3;
    }

    Mesh mesh;
    Vector3[] vertices;
    Vector2[] uv;
    int[] triangles;
    int dotCount, trianglCount, quadsCount;
    float diff = 0.1f;

    private Mesh GetQuadWaterMeshMap(bool[,] meshMap, int countOfQuads, int sizeMap)
    {
        mesh = new Mesh();
        vertices = new Vector3[4 * countOfQuads];
        uv = new Vector2[4 * countOfQuads];
        triangles = new int[countOfQuads * 6];
        dotCount = 0;
        trianglCount = 0;
        quadsCount = 0;
        for (int x = 0; x < sizeMap; x++)
        {
            for (int y = 0; y < sizeMap; y++)
            {
                if (meshMap[x, y])
                {
                    vertices[dotCount] = new Vector3(x * scale, y * scale);
                    vertices[dotCount + 1] = new Vector3(x * scale, y * scale + scale);
                    vertices[dotCount + 2] = new Vector3(x * scale + scale, y * scale + scale);
                    vertices[dotCount + 3] = new Vector3(x * scale + scale, y * scale);

                    uv[dotCount] = new Vector2(diff, diff);
                    uv[dotCount + 1] = new Vector2(diff, 1f - diff);
                    uv[dotCount + 2] = new Vector2(1f - diff, 1f - diff);
                    uv[dotCount + 3] = new Vector2(1f - diff, diff);

                    triangles[trianglCount] = dotCount;
                    triangles[trianglCount + 1] = dotCount + 1;
                    triangles[trianglCount + 2] = dotCount + 2;
                    triangles[trianglCount + 3] = dotCount;
                    triangles[trianglCount + 4] = dotCount + 2;
                    triangles[trianglCount + 5] = dotCount + 3;

                    if (x + 1 >= sizeMap || !meshMap[x + 1, y])
                    {
                        uv[dotCount + 2] += new Vector2(diff, 0);
                        uv[dotCount + 3] += new Vector2(diff, 0);
                    }

                    if (x - 1 < 0 || !meshMap[x - 1, y])
                    {
                        uv[dotCount] -= new Vector2(diff, 0);
                        uv[dotCount + 1] -= new Vector2(diff, 0);
                    }

                    if (y + 1 >= sizeMap || !meshMap[x, y + 1])
                    {
                        uv[dotCount + 1] += new Vector2(0, diff);
                        uv[dotCount + 2] += new Vector2(0, diff);
                    }

                    if (y - 1 < 0 || !meshMap[x, y - 1])
                    {
                        uv[dotCount] -= new Vector2(0, diff);
                        uv[dotCount + 3] -= new Vector2(0, diff);
                    }

                    dotCount += 4;
                    trianglCount += 6;
                    quadsCount++;
                    if (quadsCount == countOfQuads)
                    {
                        mesh.vertices = vertices;
                        mesh.uv = uv;
                        mesh.triangles = triangles;
                        return mesh;
                    }
                }
            }
        }
        return mesh;
    }
}
