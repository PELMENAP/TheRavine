using System.Collections.Generic;
using UnityEngine;

public class EndlessLiquids : IEndless
{
    private MapGenerator generator;
    private Dictionary<Vector2, ChunkData> mapData => generator.mapData;
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
                map = mapData[new Vector2(-(currentChunkCoordX + xOffset), currentChunkCoordY + yOffset)].heightMap;
                for (int x = 0; x < mapChunkSize; x++)
                    for (int y = 0; y < mapChunkSize; y++)
                    {
                        if (map[x, y] < MapGenerator.waterLevel)
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

    private Mesh GetQuadWaterMeshMap(bool[,] meshMap, int countOfQuads, int sizeMap)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[4 * countOfQuads];
        Vector2[] uv = new Vector2[4 * countOfQuads];
        int[] triangles = new int[countOfQuads * 6];
        int dotCount = 0;
        int trianglCount = 0;
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

                    uv[dotCount] = new Vector2(0.05f, 0.05f);
                    uv[dotCount + 1] = new Vector2(0.05f, 0.95f);
                    uv[dotCount + 2] = new Vector2(0.95f, 0.95f);
                    uv[dotCount + 3] = new Vector2(0.95f, 0.05f);

                    triangles[trianglCount] = dotCount;
                    triangles[trianglCount + 1] = dotCount + 1;
                    triangles[trianglCount + 2] = dotCount + 2;
                    triangles[trianglCount + 3] = dotCount;
                    triangles[trianglCount + 4] = dotCount + 2;
                    triangles[trianglCount + 5] = dotCount + 3;

                    if (x + 1 >= sizeMap || !meshMap[x + 1, y])
                    {
                        uv[dotCount + 2] += new Vector2(0.05f, 0);
                        uv[dotCount + 3] += new Vector2(0.05f, 0);
                    }

                    if (x - 1 < 0 || !meshMap[x - 1, y])
                    {
                        uv[dotCount] -= new Vector2(0.05f, 0);
                        uv[dotCount + 1] -= new Vector2(0.05f, 0);
                    }

                    if (y + 1 >= sizeMap || !meshMap[x, y + 1])
                    {
                        uv[dotCount + 1] += new Vector2(0, 0.05f);
                        uv[dotCount + 2] += new Vector2(0, 0.05f);
                    }

                    if (y - 1 < 0 || !meshMap[x, y - 1])
                    {
                        uv[dotCount] -= new Vector2(0, 0.05f);
                        uv[dotCount + 3] -= new Vector2(0, 0.05f);
                    }

                    dotCount += 4;
                    trianglCount += 6;
                }
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        return mesh;
    }
}
