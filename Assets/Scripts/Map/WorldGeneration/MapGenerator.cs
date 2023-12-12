using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class MapGenerator : MonoBehaviour, ISetAble
{
    public const byte mapChunkSize = 16, chunkCount = 3, scale = 5, generationSize = scale * mapChunkSize, waterLevel = 1;
    public const float sqrViewerMoveThresholdForChunkUpdate = 50f;
    public static Vector2 vectorOffset = new Vector2(generationSize, generationSize) * chunkCount / 2;
    private Dictionary<Vector2, ChunkData> mapData = new Dictionary<Vector2, ChunkData>(1024);
    public ChunkData GetMapData(Vector2 position)
    {
        if (!mapData.ContainsKey(position))
            mapData[position] = GenerateMapData(position);
        return mapData[position];
    }
    public Transform terrainT, waterT;
    public MeshFilter terrainF, waterF;
    public int seed;
    public Vector3 rotation;
    public ObjectSystem objectSystem;
    [SerializeField] private float noiseScale;
    [SerializeField] private byte octaves;
    [SerializeField, Range(0, 1)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private TerrainType[] regions;
    private Transform viewer;
    [SerializeField] private bool[] endlessFlag;
    private List<IEndless> endless = new List<IEndless>(4);
    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        viewer = locator.GetService<PlayerData>().transform;
        objectSystem = locator.GetService<ObjectSystem>();
        if (endlessFlag[0])
            endless.Add(new EndlessTerrain(this));
        if (endlessFlag[1])
            endless.Add(new EndlessLiquids(this));
        if (endlessFlag[2])
            endless.Add(new EndlessObjects(this));
        for (int i = -10; i < 10; i++)
        {
            for (int j = -10; j < 10; j++)
            {
                Vector2 centre = new Vector2(i, j);
                mapData[centre] = GenerateMapData(centre);
            }
        }
        position = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (scale * mapChunkSize));
        foreach (var generator in endless)
        {
            generator.UpdateChunk(position);
        }
        GenerationUpdate().Forget();
        callback?.Invoke();
    }

    private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
    // private int[] countOfHeights = new int[9];
    bool isEqual;
    float currentHeight;
    public ChunkData GenerateMapData(Vector2 centre)
    {
        List<Vector2> objectsToInst = new List<Vector2>(32);
        byte[,] heightMap = new byte[mapChunkSize, mapChunkSize];
        if (centre.x > 10000 || centre.y > 10000)
            Noise.GenerateNoiseMap(ref noiseMap, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Local);
        else
            Noise.GenerateNoiseMap(ref noiseMap, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre * mapChunkSize, Noise.NormalizeMode.Global);
        isEqual = true;
        for (byte x = 0; x < mapChunkSize; x++)
        {
            for (byte y = 0; y < mapChunkSize; y++)
            {
                currentHeight = noiseMap[x, y];
                for (byte i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                        heightMap[(mapChunkSize - x - 1), (mapChunkSize - y - 1)] = i;
                    else
                        break;
                }
                if (heightMap[x, y] != heightMap[0, 0])
                    isEqual = false;
                if (!endlessFlag[2])
                    continue;
                for (byte i = 0; i < regions[heightMap[x, y]].objects.Length; i++)
                {
                    ObjectInfoGeneration info = regions[heightMap[x, y]].objects[i];
                    if ((x * y + (byte)centre.x * centre.y + seed % 100) % info.Chance == 0)
                    {
                        Vector2 posobj = new Vector2(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale) - vectorOffset;
                        if (!objectSystem.AddToGlobal(posobj, info.info.prefab.GetInstanceID(), info.info.title, info.info.amount, info.info.iType))
                            break;
                        objectsToInst.Add(posobj);
                        break;
                    }
                }
            }
        }
        return new ChunkData(heightMap, isEqual, objectsToInst);
    }
    private Vector2 OldVposition, position;
    private async UniTaskVoid GenerationUpdate()
    {
        while (true)
        {
            if (Vector2.Distance(OldVposition, viewer.position) > sqrViewerMoveThresholdForChunkUpdate && rotateTarget == 0f)
            {
                OldVposition = viewer.position;
                GetPlayerPosition();
                foreach (var generator in endless)
                {
                    await UniTask.WaitForFixedUpdate();
                    generator.UpdateChunk(position);
                }
            }
            await UniTask.Delay(1000);
        }
    }

    public void ExtraUpdate()
    {
        if (rotateTarget != 0f)
            return;
        GetPlayerPosition();
        endless[2].UpdateChunk(position);
    }

    private void GetPlayerPosition()
    {
        position = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (scale * mapChunkSize));
    }
    private Vector2 RoundVector(Vector2 vec) => new Vector2(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
    public void RotateBasis(sbyte angle)
    {
        if (rotateValue != 0f)
            return;
        print(angle);
        if (angle == 90)
        {
            if (rotateTarget != 0f)
                return;
            RotationCome().Forget();
        }
        else if (angle == -90)
        {
            if (rotateTarget != 270f)
                return;
            RotationBack().Forget();
        }
    }
    public float rotateValue = 0f, rotateTarget = 0f;
    private static EnumerableSnapshot<int> objectsSnapshot;
    private Dictionary<int, byte> objectUpdate = new Dictionary<int, byte>(32);
    List<Vector2> chunkCoord = new List<Vector2>();
    private async UniTaskVoid RotationCome()
    {
        rotateValue = 0.1f;
        rotateTarget = 270f;
        Vector2 Vposition = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (scale * mapChunkSize));
        ObjectInfo[] prefabInfo = objectSystem._info;
        for (int i = 0; i < prefabInfo.Length; i++)
            objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
        for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                chunkCoord.Add(new Vector2(Vposition.x + xOffset, Vposition.y + yOffset));
        do
        {
            print(objectSystem.transform.eulerAngles.z);
            foreach (var vector in chunkCoord)
            {
                foreach (var item in GetMapData(vector).objectsToInst)
                {
                    ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
                    if (info.prefabID == 0)
                        continue;
                    objectUpdate[info.prefabID]++;
                    objectSystem.Reuse(info.prefabID, item, rotateValue);
                }
            }
            objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
            foreach (var ID in objectsSnapshot)
            {
                for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                    objectSystem.Deactivate(ID);
                objectUpdate[ID] = 0;
            }
            objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
            viewer.Rotate(0, 0, rotateValue, Space.Self);
            await UniTask.WaitForFixedUpdate();
        } while (objectSystem.transform.eulerAngles.z > rotateTarget);
        rotateValue = 0f;
        await UniTask.WaitForFixedUpdate();
    }

    private async UniTaskVoid RotationBack()
    {
        rotateValue = -0.1f;
        rotateTarget = 360f;
        Vector2 Vposition = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (scale * mapChunkSize));
        ObjectInfo[] prefabInfo = objectSystem._info;
        for (int i = 0; i < prefabInfo.Length; i++)
            objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
        do
        {
            print(objectSystem.transform.eulerAngles.z);
            foreach (var vector in chunkCoord)
            {
                foreach (var item in GetMapData(vector).objectsToInst)
                {
                    ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
                    if (info.prefabID == 0)
                        continue;
                    objectUpdate[info.prefabID]++;
                    print(item);
                    objectSystem.Reuse(info.prefabID, item, rotateValue);
                }
            }
            objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
            foreach (var ID in objectsSnapshot)
            {
                for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                    objectSystem.Deactivate(ID);
                objectUpdate[ID] = 0;
            }
            objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
            viewer.Rotate(0, 0, rotateValue, Space.Self);
            await UniTask.WaitForFixedUpdate();
        } while ((int)objectSystem.transform.eulerAngles.z != 0);
        rotateValue = 0f;
        rotateTarget = 0f;
        await UniTask.WaitForFixedUpdate();
    }
}

[System.Serializable]
public struct TerrainType
{
    public float height;
    public ObjectInfoGeneration[] objects;
    public StructInfoGeneration[] structs;
}

[System.Serializable]
public struct ObjectInfoGeneration
{
    public byte Chance;
    public ObjectInfo info;
}

[System.Serializable]
public struct StructInfoGeneration
{
    public byte Chance;
    public StructInfo info;
}
public class ChunkData
{
    public readonly byte[,] heightMap;
    public readonly bool isEqual;
    public List<Vector2> objectsToInst;
    public ChunkData(byte[,] heightMap, bool isEqual, List<Vector2> objectsToInst)
    {
        this.heightMap = heightMap;
        this.isEqual = isEqual;
        this.objectsToInst = objectsToInst;
    }
}