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
                    objectUpdate[prefabInfo[i].PrefabID] = 0;
                objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
            }
            public void UpdateChunk(Vector2Int Position)
            {
                ProcessObjectInst(Position);
                for (sbyte yOffset = -chunkScale; yOffset <= chunkScale; yOffset++)
                {
                    for (sbyte xOffset = -chunkScale; xOffset <= chunkScale; xOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0) continue;
                        ProcessObjectInst(new Vector2Int(Position.x + xOffset, Position.y + yOffset));
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
                    objectUpdate[info.PrefabID]++;
                    try
                    {
                        if (objectUpdate[info.PrefabID] > objectSystem.GetPoolSize(info.PrefabID))
                        {
                            objectSystem.IncreasePoolSize(info.PrefabID);
                            objectSystem.CreatePool(objectInfo.PrefabID, objectInfo.ObjectPrefab);
                        }
                    }
                    catch
                    {
                        Debug.Log(info.GetType());
                        Debug.Log(info.objectType);
                        Debug.Log(" ");
                    }
                    objectSystem.Reuse(info.PrefabID, info.realPosition);
                    if (objectInfo.BehaviourType == BehaviourType.NAL || objectInfo.BehaviourType == BehaviourType.GROW)
                        generator.AddNALObject(item);
                }
            }
        }
    }
}