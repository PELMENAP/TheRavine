using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : IEndless
{
    private MapGenerator generator;
    private const int chunkCount = MapGenerator.chunkCount, mapChunkSize = MapGenerator.mapChunkSize;
    private int scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
    private Dictionary<Vector2, ChunkData> mapData => generator.mapData;
    public EndlessTerrain(MapGenerator _generator)
    {
        generator = _generator;
    }
    private int currentChunkCoordX, currentChunkCoordY;
    private ChunkData[,] map = new ChunkData[chunkCount, chunkCount];
    public void UpdateChunk(Vector3 Vposition)
    {
        currentChunkCoordX = Mathf.RoundToInt(Vposition.x);
        currentChunkCoordY = Mathf.RoundToInt(Vposition.y);
        for (int yOffset = 0; yOffset < chunkCount; yOffset++)
            for (int xOffset = 0; xOffset < chunkCount; xOffset++)
                map[xOffset, yOffset] = mapData[new Vector2(-(currentChunkCoordX + xOffset), currentChunkCoordY + yOffset)];
        TerrainGenerator(map);
    }

    Mesh combineMesh;
    CombineInstance[] combine = new CombineInstance[chunkCount * chunkCount];
    private void TerrainGenerator(ChunkData[,] map)
    {
        int count = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            for (int j = 0; j < chunkCount; j++)
            {
                combine[count].mesh = GetTerrainMeshMap(map[i, j].heightMap, map[i, j].centre);
                combine[count].transform = Matrix4x4.TRS(new Vector3(i * generationSize, j * generationSize, 0), Quaternion.Euler(0, 0, generator.rotation.z), new Vector3(1, 1, 1)); ;
                count++;
            }
        }

        combineMesh = new Mesh();
        combineMesh.CombineMeshes(combine);
        generator.terrainF.mesh = combineMesh;
        // transform.gameObject.SetActive(true);
        generator.terrainT.position = new Vector3(currentChunkCoordX, currentChunkCoordY) * generationSize - new Vector3(generationSize, generationSize) / 2;
    }

    Mesh mesh;
    Vector3[] vertices = new Vector3[(mapChunkSize + 1) * (mapChunkSize + 1)];
    Vector2[] uv = new Vector2[(mapChunkSize + 1) * (mapChunkSize + 1)];
    int[] triangles = new int[6 * mapChunkSize * mapChunkSize];
    int trianglCount;

    private Mesh GetTerrainMeshMap(int[,] heightMap, Vector2 centre, bool isEqual = false)
    {
        if (isEqual)
        {
            mesh = new Mesh();
            vertices = new Vector3[4];
            uv = new Vector2[4];
            triangles = new int[6];
            vertices[0] = new Vector3(0, 0, heightMap[0, 0]);
            vertices[1] = new Vector3(0, generationSize, heightMap[0, 0]);
            vertices[2] = new Vector3(generationSize, generationSize, heightMap[0, 0]);
            vertices[3] = new Vector3(generationSize, 0, heightMap[0, 0]);
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            triangles[3] = 0;
            triangles[4] = 2;
            triangles[5] = 3;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            return mesh;
        }
        mesh = new Mesh();

        trianglCount = 0;
        for (int x = 0; x < mapChunkSize; x++)
            for (int y = 0; y < mapChunkSize; y++)
                vertices[x * (mapChunkSize + 1) + y] = new Vector3(x * scale, y * scale, heightMap[x, y]);

        for (int x = 0; x < mapChunkSize; x++)
            vertices[x * (mapChunkSize + 1) + mapChunkSize] = new Vector3(x * scale, generationSize, mapData[centre + new Vector2(0, -1)].heightMap[x, 0]);
        for (int y = 0; y < mapChunkSize; y++)
            vertices[mapChunkSize * (mapChunkSize + 1) + y] = new Vector3(generationSize, y * scale, mapData[centre + new Vector2(1, 0)].heightMap[0, y]);
        vertices[mapChunkSize * (mapChunkSize + 1) + mapChunkSize] = new Vector3(generationSize, generationSize, mapData[centre + new Vector2(1, -1)].heightMap[0, 0]);

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

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        return mesh;
    }
}
