using System.Collections.Generic;
using UnityEngine;

using TheRavine.ObjectControl;

namespace TheRavine.Generator.EndlessGenerators
{
    public class EndlessObjects : IEndless
    {
        private readonly MapGenerator generator;
        private const byte chunkScale = MapGenerator.chunkScale;
        private readonly ObjectSystem objectSystem;
        private readonly Dictionary<int, int> objectUpdate = new(16);
        private static EnumerableSnapshot<int> objectsSnapshot;
        public EndlessObjects(MapGenerator _generator, ObjectSystem _objectSystem)
        {
            generator = _generator;
            objectSystem = _objectSystem;
            ObjectInfo[] prefabInfo = objectSystem._info;
            for (int i = 0; i < prefabInfo.Length; i++)
                objectUpdate[prefabInfo[i].PrefabID] = 0;
            objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        }
        public void UpdateChunk(Vector2Int Position)
        {
            for (int chunkX = 0; chunkX < 2 * chunkScale + 1; chunkX++)
            {
                for (int chunkY = 0; chunkY < 2 * chunkScale + 1; chunkY++)
                {
                    generator.GetMapData(new Vector2Int(Position.x - 1 + chunkX, Position.y - 1 + chunkY));
                }
            }

            for (int chunkX = 0; chunkX < 2 * chunkScale + 1; chunkX++)
            {
                for (int chunkY = 0; chunkY < 2 * chunkScale + 1; chunkY++)
                {
                    ProcessObjectInst(new Vector2Int(Position.x - 1 + chunkX, Position.y - 1 + chunkY));
                }
            }
            foreach (var ID in objectsSnapshot)
            {
                if (objectUpdate[ID] == 0)
                    continue;
                for (int j = 0; j < objectSystem.GetPoolSize(ID) - objectUpdate[ID]; j++)
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
                    Debug.Log(info.Type);
                }
                objectSystem.Reuse(info.PrefabID, info.Position);
                if (objectInfo.BehaviourType == BehaviourType.NAL || objectInfo.BehaviourType == BehaviourType.GROW)
                    generator.AddNALObject(item);
            }
        }
    }
}