using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class MapGenerator : MonoBehaviour
{
    public Dictionary<Vector2, ChunkData> mapData = new Dictionary<Vector2, ChunkData>();
    public Transform terrainT, waterT;
    public MeshFilter terrainF, waterF;
    public int seed;
    public const int mapChunkSize = 16, chunkCount = 3, scale = 5, generationSize = scale * mapChunkSize, waterLevel = 1;
    public const float sqrViewerMoveThresholdForChunkUpdate = 40f;
    public Vector3 rotation;
    [SerializeField] private float noiseScale;
    [SerializeField] private int octaves;
    [SerializeField, Range(0, 1)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private TerrainType[] regions;
    [SerializeField] private Transform viewer;
    [SerializeField] private bool[] endlessFlag;
    private List<IEndless> endless = new List<IEndless>();

    [Button]
    private void Test()
    {
        SetUp();
        Vector3 position = new Vector3(viewer.position.x, viewer.position.y, 0) / (scale * mapChunkSize);
        foreach (var generator in endless)
            generator.UpdateChunk(position);
    }

    public GameObject prefab;
    public void SetUp()
    {
        if (endlessFlag[0])
            endless.Add(new EndlessTerrain(this));
        if (endlessFlag[1])
            endless.Add(new EndlessLiquids(this));
        if (endlessFlag[2])
            endless.Add(new EndlessObjects(this));
        Vector2 centre;
        for (int i = -10; i < 10; i++)
        {
            for (int j = -10; j < 10; j++)
            {
                centre = new Vector2(i, j);
                mapData[centre] = GenerateMapData(centre);
            }
        }
        position = new Vector3(viewer.position.x, viewer.position.y, 0) / (scale * mapChunkSize);
        foreach (var generator in endless)
            generator.UpdateChunk(position);
        StartCoroutine(GenerationUpdate());
    }

    private float[,] noiseMap;
    private Color[] colourMap;
    private int[,] heightMap;
    public ChunkData GenerateMapData(Vector2 centre)
    {
        noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Global);
        colourMap = new Color[mapChunkSize * mapChunkSize];
        heightMap = new int[mapChunkSize, mapChunkSize];
        float currentHeight;
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].colour;
                        heightMap[x, y] = i;
                    }
                    else
                        break;
                }
            }
        }
        return new ChunkData(noiseMap, colourMap, heightMap, centre);
    }

    private Vector3 OldVposition, position;
    private IEnumerator GenerationUpdate()
    {
        if ((OldVposition - viewer.position).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
        {
            OldVposition = viewer.position;
            position = new Vector3(viewer.position.x, viewer.position.y, 0) / (scale * mapChunkSize);
            foreach (var generator in endless)
            {
                yield return new WaitForFixedUpdate();
                generator.UpdateChunk(position);
            }
        }
        yield return new WaitForFixedUpdate();
    }
}

[System.Serializable]
public struct TerrainType
{
    public float height;
    public Color colour;

}

public struct ChunkData
{
    public readonly float[,] noiseMap;
    public readonly Color[] colourMap;
    public readonly int[,] heightMap;
    public readonly Vector2 centre;
    public ChunkData(float[,] noiseMap, Color[] colourMap, int[,] heightMap, Vector2 centre)
    {
        this.noiseMap = noiseMap;
        this.colourMap = colourMap;
        this.heightMap = heightMap;
        this.centre = centre;
    }
}