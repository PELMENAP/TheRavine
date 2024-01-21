using System.Collections.Generic;
using UnityEngine;
using TheRavine.Extentions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessObjects : IEndless
        {
            private MapGenerator generator;
            private const byte chunkCount = MapGenerator.chunkCount;
            private ObjectSystem objectSystem;
            private Dictionary<int, ushort> objectUpdate = new Dictionary<int, ushort>(16);
            public EndlessObjects(MapGenerator _generator)
            {
                generator = _generator;
                objectSystem = generator.objectSystem;
                ObjectInfo[] prefabInfo = objectSystem._info;
                for (ushort i = 0; i < prefabInfo.Length; i++)
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
                            if (!objectSystem.ContainsGlobal(item) || info.prefabID == -1)
                                continue;
                            objectUpdate[info.prefabID]++;
                            ObjectInfo objectInfo = objectSystem.GetPrefabInfo(info.prefabID);
                            if (objectUpdate[info.prefabID] > objectSystem.GetPoolSize(info.prefabID))
                            {
                                objectSystem.IncreasePoolSize(info.prefabID);
                                objectSystem.CreatePool(objectInfo.prefab);
                            }
                            objectSystem.Reuse(info.prefabID, item, info.flip, generator.rotateValue);
                            if (objectInfo.bType == BehaviourType.NAL || objectInfo.bType == BehaviourType.GROW)
                                generator.AddNALObject(item);
                        }
                    }
                }
                objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
                foreach (var ID in objectsSnapshot)
                {
                    for (ushort j = 0; j < objectSystem.GetPoolSize(ID) - objectUpdate[ID]; j++)
                        objectSystem.Deactivate(ID);
                    objectUpdate[ID] = 0;
                }
            }

            private Texture2D texture;
            private Texture2D TextureFromColourMap(Color[] colourMap, byte width, byte height)
            {
                texture = new Texture2D(width, height);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels(colourMap);
                texture.Apply();
                return texture;
            }
        }
    }
}