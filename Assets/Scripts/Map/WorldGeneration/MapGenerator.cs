using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class MapGenerator : MonoBehaviour
{
    private Dictionary<Vector2, ChunkData> mapData = new Dictionary<Vector2, ChunkData>();

    public ChunkData GetMapData(Vector2 position)
    {
        if (!mapData.ContainsKey(position))
            mapData[position] = GenerateMapData(position);
        return mapData[position];
    }
    public Transform terrainT, waterT;
    public MeshFilter terrainF, waterF;
    public int seed;
    public const int mapChunkSize = 16, chunkCount = 3, scale = 5, generationSize = scale * mapChunkSize, waterLevel = 1;
    public const float sqrViewerMoveThresholdForChunkUpdate = 60f;
    public static Vector2 vectorOffset = new Vector2(generationSize, generationSize) * 3 / 2;
    public Vector3 rotation;
    [SerializeField] private float noiseScale;
    [SerializeField] private int octaves;
    [SerializeField, Range(0, 1)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private TerrainType[] regions;
    [SerializeField] private Transform viewer;
    [SerializeField] private bool[] endlessFlag;
    private List<IEndless> endless = new List<IEndless>();

    // private void Start()
    // {
    //     SetUp();
    // }

    [Button]
    private void Test()
    {
        if (endlessFlag[0])
            endless.Add(new EndlessTerrain(this));
        if (endlessFlag[1])
            endless.Add(new EndlessLiquids(this));
        if (endlessFlag[2])
            endless.Add(new EndlessObjects(this));
        Vector2 centre;
        for (int i = -20; i < 20; i++)
        {
            for (int j = -20; j < 20; j++)
            {
                centre = new Vector2(-i, j);
                mapData[centre] = GenerateMapData(centre);
            }
        }
        Vector3 position = new Vector3(viewer.position.x, viewer.position.y, 0) / (scale * mapChunkSize);
        foreach (var generator in endless)
            generator.UpdateChunk(position);
    }
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
        foreach (var generator in endless)
        {
            generator.UpdateChunk(position);
        }
        StartCoroutine(GenerationUpdate());
    }

    private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
    private int[] countOfHeights = new int[9];
    bool isEqual;
    float currentHeight;
    public ChunkData GenerateMapData(Vector2 centre)
    {
        List<Vector2> objectsToInst = new List<Vector2>();
        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
        int[,] heightMap = new int[mapChunkSize, mapChunkSize];
        Noise.GenerateNoiseMap(ref noiseMap, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Global);
        isEqual = true;
        for (int x = 0; x < mapChunkSize; x++)
        {
            for (int y = 0; y < mapChunkSize; y++)
            {
                currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].colour;
                        heightMap[(mapChunkSize - x - 1), (mapChunkSize - y - 1)] = i;
                    }
                    else
                        break;
                }
                if (heightMap[x, y] != heightMap[0, 0])
                    isEqual = false;
                if (!endlessFlag[2])
                    continue;
                for (int i = 0; i < regions[heightMap[x, y]].objects.Length; i++)
                {
                    ObjectInfoGeneration info = regions[heightMap[x, y]].objects[i];
                    if ((x * y + (int)centre.x * centre.y + seed % 100) % info.Chance == 0)
                    {
                        Vector2 posobj = new Vector2(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale) - vectorOffset;
                        int id = info.Prefab.GetInstanceID();
                        PrefabInfo prInfo = ObjectSystem.inst.GetPrefabInfo(id);
                        if (!ObjectSystem.inst.AddToGlobal(posobj, id, prInfo.name, prInfo.amount, prInfo.type))
                            break;
                        objectsToInst.Add(posobj);
                        break;
                    }
                }
            }
        }
        return new ChunkData(colourMap, heightMap, centre, isEqual, objectsToInst);
    }



    private Vector3 OldVposition, position;
    private IEnumerator GenerationUpdate()
    {
        while (true)
        {
            if (Vector3.Distance(OldVposition, viewer.position) > sqrViewerMoveThresholdForChunkUpdate)
            {
                OldVposition = viewer.position;
                position = new Vector3(viewer.position.x, viewer.position.y, 0) / (scale * mapChunkSize);
                foreach (var generator in endless)
                {
                    yield return new WaitForFixedUpdate();
                    generator.UpdateChunk(position);
                }
                print("update");
            }
            yield return new WaitForFixedUpdate();
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public float height;
    public Color colour;
    public ObjectInfoGeneration[] objects;
}

[System.Serializable]
public struct ObjectInfoGeneration
{
    public int Chance;
    public GameObject Prefab;
}
public enum InstanceType
{
    Static,
    Inter
}
public struct ChunkData
{
    public readonly Color[] colourMap;
    public readonly int[,] heightMap;
    public readonly Vector2 centre;
    public readonly bool isEqual;
    public readonly List<Vector2> objectsToInst;

    public ChunkData(Color[] colourMap, int[,] heightMap, Vector2 centre, bool isEqual, List<Vector2> objectsToInst)
    {
        this.colourMap = colourMap;
        this.heightMap = heightMap;
        this.centre = centre;
        this.isEqual = isEqual;
        this.objectsToInst = objectsToInst;
    }
}