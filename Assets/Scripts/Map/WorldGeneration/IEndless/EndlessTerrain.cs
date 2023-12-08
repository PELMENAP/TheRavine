using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : IEndless
{
    private MapGenerator generator;
    private const byte chunkCount = MapGenerator.chunkCount, mapChunkSize = MapGenerator.mapChunkSize;
    private byte scale = MapGenerator.scale, generationSize = MapGenerator.generationSize;
    private Vector2 vectorOffset = MapGenerator.vectorOffset;
    public EndlessTerrain(MapGenerator _generator)
    {
        generator = _generator;
    }
    private ChunkData[,] map = new ChunkData[chunkCount, chunkCount];
    public void UpdateChunk(Vector2 Vposition)
    {
        for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                map[xOffset, yOffset] = generator.GetMapData(new Vector2(Vposition.x + xOffset, Vposition.y + yOffset));
        TerrainGenerator(map, Vposition);
    }

    Mesh combineMesh;
    CombineInstance[] combine = new CombineInstance[chunkCount * chunkCount];
    byte count;
    private void TerrainGenerator(ChunkData[,] map, Vector2 Vposition)
    {
        count = 0;
        for (byte i = 0; i < chunkCount; i++)
        {
            for (byte j = 0; j < chunkCount; j++)
            {
                combine[count].mesh = GetTerrainMeshMap(map[i, j].heightMap, new Vector2(Vposition.x + i, Vposition.y + j), map[i, j].isEqual);
                combine[count].transform = Matrix4x4.TRS(new Vector3(i * generationSize, j * generationSize, 0), Quaternion.Euler(0, 0, generator.rotation.z), new Vector3(1, 1, 1)); ;
                count++;
            }
        }

        combineMesh = new Mesh();
        combineMesh.CombineMeshes(combine);
        generator.terrainF.mesh = combineMesh;
        // transform.gameObject.SetActive(true);
        generator.terrainT.position = new Vector2(Vposition.x, Vposition.y) * generationSize - vectorOffset;
    }
    Vector3[] vertices = new Vector3[(mapChunkSize + 1) * (mapChunkSize + 1)], Mvertices = new Vector3[4];
    Vector2[] uv = new Vector2[(mapChunkSize + 1) * (mapChunkSize + 1)], Muv = new Vector2[4];
    int[] triangles = new int[6 * mapChunkSize * mapChunkSize], Mtriangles = new int[6];

    private Mesh GetTerrainMeshMap(byte[,] heightMap, Vector2 centre, bool isEqual)
    {
        Mesh mesh = new Mesh();
        if (isEqual)
        {
            Mvertices[0] = new Vector3(0, 0, heightMap[0, 0]);
            Mvertices[1] = new Vector3(0, generationSize, heightMap[0, 0]);
            Mvertices[2] = new Vector3(generationSize, generationSize, heightMap[0, 0]);
            Mvertices[3] = new Vector3(generationSize, 0, heightMap[0, 0]);
            Mtriangles[0] = 0;
            Mtriangles[1] = 1;
            Mtriangles[2] = 2;
            Mtriangles[3] = 0;
            Mtriangles[4] = 2;
            Mtriangles[5] = 3;
            mesh.vertices = Mvertices;
            mesh.uv = Muv;
            mesh.triangles = Mtriangles;
            return mesh;
        }

        ushort trianglCount = 0;
        for (byte x = 0; x < mapChunkSize; x++)
            for (byte y = 0; y < mapChunkSize; y++)
                vertices[x * (mapChunkSize + 1) + y] = new Vector3(x * scale, y * scale, heightMap[x, y]);

        for (byte x = 0; x < mapChunkSize; x++)
            vertices[x * (mapChunkSize + 1) + mapChunkSize] = new Vector3(x * scale, generationSize, generator.GetMapData(centre + new Vector2(0, 1)).heightMap[x, 0]);
        for (byte y = 0; y < mapChunkSize; y++)
            vertices[mapChunkSize * (mapChunkSize + 1) + y] = new Vector3(generationSize, y * scale, generator.GetMapData(centre + new Vector2(1, 0)).heightMap[0, y]);
        vertices[mapChunkSize * (mapChunkSize + 1) + mapChunkSize] = new Vector3(generationSize, generationSize, generator.GetMapData(centre + new Vector2(1, 1)).heightMap[0, 0]);

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

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        return mesh;
    }
}
