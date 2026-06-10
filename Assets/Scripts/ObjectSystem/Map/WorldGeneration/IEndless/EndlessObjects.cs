using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.ObjectControl;
using TheRavine.Extensions;

namespace TheRavine.Generator.EndlessGenerators
{
    public sealed class EndlessObjects : IEndless
    {
        private readonly MapGenerator generator;
        private readonly ObjectSystem  objectSystem;
        private const byte chunkScale = MapGenerator.chunkScale;
        private readonly Dictionary<int, int> objectUpdate = new(16);
        private EnumerableSnapshot<int> objectsSnapshot;

        public EndlessObjects(MapGenerator generator, ObjectSystem objectSystem)
        {
            this.generator    = generator;
            this.objectSystem = objectSystem;

            var infos = objectSystem.infoRegistry.objectInfos;
            for (int i = 0; i < infos.Count; i++)
                objectUpdate[infos[i].PrefabID] = 0;

            objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        }

        public async UniTaskVoid UpdateChunk(Vector2Int position)
        {
            int side = 2 * chunkScale + 1;

            for (int cx = 0; cx < side; cx++)
            for (int cy = -1; cy < side - 1; cy++)
                ProcessChunk(Position2Int.Pack(
                    position.x - chunkScale + cx,
                    position.y - chunkScale + cy));

            foreach (int id in objectsSnapshot)
            {
                if (objectUpdate[id] == 0) continue;

                int excess = objectSystem.GetPoolSize(id) - objectUpdate[id];
                for (int j = 0; j < excess; j++)
                    objectSystem.Deactivate(id);

                objectUpdate[id] = 0;
            }
            await UniTask.CompletedTask;
        }

        private void ProcessChunk(long chunkCoord)
        {
            ChunkData cd = generator.GetMapData(chunkCoord);

            for (int i = 0; i < cd.Objects.Length; i++)
            {
                ObjectInstInfo info = cd.Objects[i];
                if (info.PrefabID <= 0) continue;

                ObjectInfo objectInfo = objectSystem.GetInfo(info.PrefabID);
                if (objectInfo == null) continue;

                objectUpdate[info.PrefabID]++;

                if (objectUpdate[info.PrefabID] > objectSystem.GetPoolSize(info.PrefabID))
                {
                    objectSystem.IncreasePoolSize(info.PrefabID);
                    objectSystem.CreatePool(info.PrefabID, objectInfo.ObjectPrefab);
                }

                objectSystem.Reuse(info.PrefabID, info.Position);

                if (objectInfo.BehaviourType == BehaviourType.NAL ||
                    objectInfo.BehaviourType == BehaviourType.GROW)
                {
                    var worldPos = new Vector2Int(
                        Mathf.RoundToInt(info.Position.x),
                        Mathf.RoundToInt(info.Position.z));

                    generator.AddNALObject(worldPos);
                }
            }
        }
    }
}