using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extensions;
using TheRavine.Services;
using TheRavine.Base;

namespace TheRavine.EntityControl
{
    public class MobGenerator : MonoBehaviour, ISetAble
    {
        private System.Threading.CancellationTokenSource _cts = new();
        private const int chunkScale = MapGenerator.chunkScale;
        private GameObject CreateMob(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        [SerializeField] private SpawnPointDataHeight[] regions;
        [SerializeField, Min(0)] private ushort MaxSpawnEntityCount;
        private AEntity player;
        private MapGenerator generator;
        private MobController mobsController;
        private EntitySystem entitySystem;
        private readonly Dictionary<Vector2Int, ChunkEntityData> mapData = new(4);
        private ChunkEntityData GetMapData(Vector2Int pos)
        {
            if (!mapData.ContainsKey(pos))
                mapData[pos] = new ChunkEntityData();
            return mapData[pos];
        }
        private Vector2Int currentChunkPosition, oldChunkPosition;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            generator = locator.GetService<MapGenerator>();
            mobsController = locator.GetService<MobController>();
            if(generator != null)
            {
                generator.onSpawnPoint += AddSpawnPoint;
                generator.onUpdate += UpdateNALQueue;
            }
            UpdateNAL().Forget();

            entitySystem = locator.GetService<EntitySystem>();

            NAL().Forget();

            callback?.Invoke();
        }

        private Queue<Pair<Vector2Int, Pair<int, int>>> NALQueue;
        private Queue<Pair<Vector2Int, GameObject>> NALQueueUpdate;
        public void ClearNALQueue() => NALQueue.Clear();
        [SerializeField] private int step;
        private async UniTaskVoid NAL() // natural artificial life
        {
            NALQueue = new Queue<Pair<Vector2Int, Pair<int, int>>>(8);
            NALQueueUpdate = new Queue<Pair<Vector2Int, GameObject>>(8);
            await UniTask.Delay(5000);
            bool NALthread = true;
            int countCycle = 0;
            while (NALthread)
            {
                countCycle++;
                if (NALQueue.Count == 0)
                {
                    await UniTask.Delay(5000, cancellationToken: _cts.Token);
                    continue;
                }
                if (countCycle % step == 0)
                {
                    NALQueue.Enqueue(NALQueue.Dequeue());
                    await UniTask.Delay(1000, cancellationToken: _cts.Token);
                    continue;
                }
                Pair<Vector2Int,  Pair<int, int>> current = NALQueue.Dequeue();
                MobSpawnData[] currentEntities = regions[current.Second.First].temperatureLevels[current.Second.Second].entities;
                for (int i = 0; i < currentEntities.Length; i++)
                {
                    MobSpawnData curMobSpawnData = currentEntities[i];
                    if(curMobSpawnData.Chance <= 0)
                        continue;
                    if (RavineRandom.Hundred() < curMobSpawnData.Chance)
                    {
                        NALQueueUpdate.Enqueue(new Pair<Vector2Int, GameObject>(current.First, curMobSpawnData.info.prefab));
                        await UniTask.Delay(curMobSpawnData.Chance * curMobSpawnData.Chance, cancellationToken: _cts.Token);
                        break;
                    }
                }
                NALQueue.Enqueue(current);
                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }
        public async UniTaskVoid UpdateNAL()
        {
            while (!DataStorage.sceneClose)
            {
                await UniTask.Delay(100000);
                ClearNALQueue();
                while (NALQueueUpdate.Count > 0 && mobsController.GetEntityCount() < MaxSpawnEntityCount)
                {
                    Pair<Vector2Int, GameObject> item = NALQueueUpdate.Dequeue();
                    // GameObject curMob = CreateMob(Extension.GetRandomPointAround(item.First, 2), item.Second);
                    // AEntity entity = curMob.GetComponentInChildren<AEntity>();
                    // entity.SetUpEntityData(entitySystem.GetMobInfo(item.Second.GetInstanceID()));
                    // entity.Init();
                    // GetMapData(generator.GetChunkPosition(item.First)).entitiesInChunk.Add(entity);
                    Debug.Log("forget to create a MobEntity");
                }
                UpdateNALQueue(currentChunkPosition);
            }
        }
        private void AddSpawnPoint(Vector2Int position, int height, int temperature, Vector2Int chunkCenter)
        {
            GetMapData(chunkCenter).spawnPoints[position] = new Pair<int, int>(height, temperature);
        }
        private void UpdateNALQueue(Vector2Int position)
        {
            for (int yOffset = -chunkScale; yOffset < chunkScale; yOffset++)
            {
                for (int xOffset = -chunkScale; xOffset < chunkScale; xOffset++)
                {
                    List<AEntity> listEntity = GetMapData(currentChunkPosition + new Vector2Int(xOffset, yOffset)).entitiesInChunk;
                    for (ushort i = 0; i < listEntity.Count; i++)
                    {
                        AEntity entity = listEntity[i];
                        GetMapData(generator.GetChunkPosition(entity.GetEntityComponent<TransformComponent>().GetEntityPosition())).entitiesInChunk.Add(entity);
                        entity.Deactivate();
                        mobsController.RemoveMobFromUpdate(entity);
                        listEntity.Remove(entity);
                    }
                }
            }
            currentChunkPosition = position;
            for (int yOffset = -chunkScale; yOffset < chunkScale; yOffset++)
            {
                for (int xOffset = -chunkScale; xOffset < chunkScale; xOffset++)
                {
                    ChunkEntityData data = GetMapData(currentChunkPosition + new Vector2Int(xOffset, yOffset));
                    foreach (var item in data.spawnPoints)
                        NALQueue.Enqueue(new Pair<Vector2Int, Pair<int, int>>(item.Key, item.Value));
                    List<AEntity> listEntity = data.entitiesInChunk;
                    for (ushort i = 0; i < listEntity.Count; i++)
                    {
                        AEntity entity = listEntity[i];
                        TransformComponent transformComponent = entity.GetEntityComponent<TransformComponent>();
                        if (xOffset == 0 || yOffset == 0 || xOffset == 2 * chunkScale || yOffset == 2 * chunkScale )
                            transformComponent.GetEntityTransform().position = Extension.GetRandomPointAround(transformComponent.GetEntityPosition(), 2);
                        mobsController.AddMobToUpdate(entity);
                        entity.Activate();
                    }
                }
            }
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            if(generator != null)
            {
                generator.onSpawnPoint -= AddSpawnPoint;
                generator.onUpdate -= UpdateNALQueue;
            }
            ClearNALQueue();
            NALQueueUpdate.Clear();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            _cts.Cancel();
            mapData.Clear();
        }
    }

    public class ChunkEntityData
    {
        public Dictionary<Vector2Int, Pair<int, int>> spawnPoints;
        public List<AEntity> entitiesInChunk;
        public ChunkEntityData()
        {
            spawnPoints = new Dictionary<Vector2Int, Pair<int, int>>();
            entitiesInChunk = new List<AEntity>();
        }
    }

    [System.Serializable]
    public struct SpawnPointDataHeight
    {
        public SpawnPointDataTemperatureLevel[] temperatureLevels;
    }

    [System.Serializable]
    public struct SpawnPointDataTemperatureLevel{
        public MobSpawnData[] entities;
    }

    [System.Serializable]
    public struct MobSpawnData
    {
        public int Chance;
        public EntityInfo info;
    }
}