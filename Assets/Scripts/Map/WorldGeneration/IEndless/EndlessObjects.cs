using System.Collections.Generic;
using UnityEngine;

public class EndlessObjects : IEndless
{
    private MapGenerator generator;
    private const byte chunkCount = MapGenerator.chunkCount;
    private ObjectSystem objectSystem;
    private Dictionary<int, byte> objectUpdate = new Dictionary<int, byte>(32);
    public EndlessObjects(MapGenerator _generator)
    {
        generator = _generator;
        objectSystem = generator.objectSystem;
        ObjectInfo[] prefabInfo = objectSystem._info;
        for (int i = 0; i < prefabInfo.Length; i++)
            objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
    }

    private static EnumerableSnapshot<int> objectsSnapshot;
    public void UpdateChunk(Vector2 Vposition)
    {
        for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
        {
            for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
            {
                Vector2 chunkCoord = new Vector2(Vposition.x + xOffset, Vposition.y + yOffset);
                foreach (var item in generator.GetMapData(chunkCoord).objectsToInst)
                {
                    ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
                    if (info.prefabID == 0)
                        continue;
                    objectUpdate[info.prefabID]++;
                    PrefabData objectInfo = objectSystem.GetPrefabInfo(info.prefabID);
                    if (objectUpdate[info.prefabID] > objectInfo.poolSize)
                    {
                        objectInfo.poolSize++;
                        objectSystem.CreatePool(objectInfo.prefab, 1);
                    }
                    objectSystem.Reuse(info.prefabID, item, generator.rotateValue);
                }
            }
        }
        objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        foreach (var ID in objectsSnapshot)
        {
            for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
                objectSystem.Deactivate(ID);
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