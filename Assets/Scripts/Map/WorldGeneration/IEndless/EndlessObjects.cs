using System.Collections.Generic;
using UnityEngine;

public class EndlessObjects : IEndless
{
    private MapGenerator generator;
    private const int chunkCount = MapGenerator.chunkCount;
    private Vector2[] terrainChunksVisibleUpdate = new Vector2[chunkCount * chunkCount];
    private PrefabInfo[] prefabInfo = ObjectSystem.inst._info;
    private Dictionary<int, byte> objectUpdate;
    public EndlessObjects(MapGenerator _generator)
    {
        generator = _generator;
        objectUpdate = new Dictionary<int, byte>();
        for (int i = 0; i < prefabInfo.Length; i++)
            objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
    }

    private static EnumerableSnapshot<int> objectsSnapshot;
    public void UpdateChunk(Vector3 Vposition)
    {
        int currentChunkCoordX = Mathf.RoundToInt(Vposition.x);
        int currentChunkCoordY = Mathf.RoundToInt(Vposition.y);
        int countOfIteration = 0;
        for (int yOffset = 0; yOffset < chunkCount; yOffset++)
        {
            for (int xOffset = 0; xOffset < chunkCount; xOffset++)
            {
                Vector2 chunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                terrainChunksVisibleUpdate[countOfIteration] = chunkCoord;
                List<Vector2> map = generator.GetMapData(chunkCoord).objectsToInst;
                foreach (var item in map)
                {
                    ObjectInstInfo info = ObjectSystem.inst.GetGlobalObjectInfo(item);
                    if (info.name == "therivinetop")
                        continue;
                    objectUpdate[info.prefabID]++;
                    PrefabData objectInfo = ObjectSystem.inst.GetPrefabInfo(info.prefabID);
                    if (objectUpdate[info.prefabID] > objectInfo.poolSize)
                    {
                        objectInfo.poolSize++;
                        ObjectSystem.inst.CreatePool(objectInfo.prefab, 1);
                    }
                    ObjectSystem.inst.Reuse(info.prefabID, item);
                }
                countOfIteration++;
            }
        }
        objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        foreach (var ID in objectsSnapshot)
        {
            for (int j = 0; j < ObjectSystem.inst.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                ObjectSystem.inst.Deactivate(ID);
            objectUpdate[ID] = 0;
        }
    }

    private Texture2D texture;
    private Texture2D TextureFromColourMap(Color[] colourMap, int width, int height)
    {
        texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        return texture;
    }
}