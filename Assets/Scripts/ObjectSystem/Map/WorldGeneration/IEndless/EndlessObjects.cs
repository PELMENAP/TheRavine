using System.Collections.Generic;
using UnityEngine;

using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessObjects : IEndless
        {
            private readonly MapGenerator generator;
            private const byte chunkScale = MapGenerator.chunkScale;
            private readonly ObjectSystem objectSystem;
            private readonly Dictionary<int, ushort> objectUpdate = new(16);
            private static EnumerableSnapshot<int> objectsSnapshot;
            public EndlessObjects(MapGenerator _generator, ObjectSystem _objectSystem)
            {
                generator = _generator;
                objectSystem = _objectSystem;
                ObjectInfo[] prefabInfo = objectSystem._info;
                for (ushort i = 0; i < prefabInfo.Length; i++)
                    objectUpdate[prefabInfo[i].prefabID] = 0;
                objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
            }
            public void UpdateChunk(Vector2Int Vposition)
            {
                ProcessObjectInst(Vposition);
                for (sbyte yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                {
                    for (sbyte xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0) continue;
                        ProcessObjectInst(new Vector2Int(Vposition.x + xOffset, Vposition.y + yOffset));
                    }
                }
                foreach (var ID in objectsSnapshot)
                {
                    if (objectUpdate[ID] == 0)
                        continue;
                    for (ushort j = 0; j < objectSystem.GetPoolSize(ID) - objectUpdate[ID]; j++)
                        objectSystem.Deactivate(ID);
                    objectUpdate[ID] = 0;
                }
            }

            private void ProcessObjectInst(Vector2Int chunkCoord)
            {
                foreach (var item in generator.GetMapData(chunkCoord).objectsToInst)
                {
                    ObjectInstInfo info = objectSystem.GetGlobalObjectInstInfo(item);
                    ObjectInfo objectInfo = objectSystem.GetGlobalObjectInfo(item);
                    if(objectInfo == null) continue;
                    objectUpdate[info.prefabID]++;
                    try
                    {
                        if (objectUpdate[info.prefabID] > objectSystem.GetPoolSize(info.prefabID))
                        {
                            objectSystem.IncreasePoolSize(info.prefabID);
                            objectSystem.CreatePool(objectInfo.prefabID, objectInfo.prefab);
                        }
                    }
                    catch
                    {
                        Debug.Log(info.GetType());
                        Debug.Log(info.objectType);
                        Debug.Log(" ");
                    }
                    objectSystem.Reuse(info.prefabID, item, info.flip, generator.rotateValue);
                    if (objectInfo.bType == BehaviourType.NAL || objectInfo.bType == BehaviourType.GROW)
                        generator.AddNALObject(item);
                }
            }

            private Texture2D texture;
            private Texture2D TextureFromColourMap(Color[] colourMap, byte width, byte height)
            {
                texture = new Texture2D(width, height)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels(colourMap);
                texture.Apply();
                return texture;
            }
        }
    }
}