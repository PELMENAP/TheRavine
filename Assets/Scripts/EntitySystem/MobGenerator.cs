using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extentions;
using TheRavine.Services;
using TheRavine.Base;

namespace TheRavine.EntityControl
{
    public class MobGenerator : MonoBehaviour, ISetAble
    {
        private System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();
        private const byte chunkCount = MapGenerator.chunkCount;
        private GameObject CreateMob(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        [SerializeField] private SpawnPointDataHeight[] regions;
        [SerializeField, Min(0)] private ushort MaxSpawnEntityCount;
        private AEntity player;
        private MapGenerator generator;
        private MobController mobsController;
        private EntitySystem entitySystem;
        private Dictionary<Vector2, ChunkEntityData> mapData = new Dictionary<Vector2, ChunkEntityData>(4);
        private ChunkEntityData GetMapData(Vector2 pos)
        {
            if (!mapData.ContainsKey(pos))
                mapData[pos] = new ChunkEntityData();
            return mapData[pos];
        }
        private Vector2 currentChunkPosition, oldChunkPosition;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            generator = locator.GetService<MapGenerator>();
            mobsController = locator.GetService<MobController>();
            generator.onSpawnPoint += AddSpawnPoint;
            generator.onUpdate += UpdateNALQueue;
            DayCycle.newDay += UpdateNAL;

            player = locator.GetService<PlayerEntity>();
            entitySystem = locator.GetService<EntitySystem>();
            entitySystem.AddToGlobal(player);

            NAL().Forget();

            callback?.Invoke();
        }

        private Queue<Pair<Vector2, Pair<byte, byte>>> NALQueue;
        private Queue<Pair<Vector2, GameObject>> NALQueueUpdate;
        public void ClearNALQueue() => NALQueue.Clear();
        [SerializeField] private byte step;
        private async UniTaskVoid NAL()
        {
            NALQueue = new Queue<Pair<Vector2, Pair<byte, byte>>>(8);
            NALQueueUpdate = new Queue<Pair<Vector2, GameObject>>(8);
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
                Pair<Vector2,  Pair<byte, byte>> current = NALQueue.Dequeue();
                MobSpawnData[] currentEnities = regions[current.Second.First].temperatureLevels[current.Second.Second].entities;
                for (byte i = 0; i < currentEnities.Length; i++)
                {
                    MobSpawnData curMobSpawnData = currentEnities[i];
                    if(curMobSpawnData.Chance <= 0)
                        continue;
                    if (RavineRandom.Hundred() < curMobSpawnData.Chance)
                    {
                        print("summon somebody");
                        NALQueueUpdate.Enqueue(new Pair<Vector2, GameObject>(current.First, curMobSpawnData.info.prefab));
                        await UniTask.Delay(curMobSpawnData.Chance * curMobSpawnData.Chance, cancellationToken: _cts.Token);
                        break;
                    }
                }
                NALQueue.Enqueue(current);
                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }
        public void UpdateNAL()
        {
            ClearNALQueue();
            while (NALQueueUpdate.Count > 0 && mobsController.GetEntityCount() < MaxSpawnEntityCount)
            {
                Pair<Vector2, GameObject> item = NALQueueUpdate.Dequeue();
                GameObject curMob = CreateMob(Extention.GetRandomPointAround(item.First, 2), item.Second);
                AEntity entity = curMob.GetComponentInChildren<AEntity>();
                entity.SetUpEntityData(entitySystem.GetMobInfo(item.Second.GetInstanceID()));
                entity.Init();
                GetMapData(generator.GetChunkPosition(item.First)).entitiesInChunk.Add(entity);
            }
            UpdateNALQueue(currentChunkPosition);
        }
        private void AddSpawnPoint(Vector2 position, byte height, byte temperature, Vector2 chunkCenter)
        {
            GetMapData(chunkCenter).spawnPoints[position] = new Pair<byte, byte>(height, temperature);
        }
        private void UpdateNALQueue(Vector2 position)
        {
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    List<AEntity> listEntity = GetMapData(currentChunkPosition + new Vector2(xOffset, yOffset)).entitiesInChunk;
                    for (ushort i = 0; i < listEntity.Count; i++)
                    {
                        AEntity entity = listEntity[i];
                        GetMapData(generator.GetChunkPosition(entity.GetEntityPosition())).entitiesInChunk.Add(entity);
                        entity.DisableView();
                        mobsController.RemoveMobFromUpdate(entity);
                        listEntity.Remove(entity);
                    }
                }
            }
            currentChunkPosition = position;
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    ChunkEntityData data = GetMapData(currentChunkPosition + new Vector2(xOffset, yOffset));
                    foreach (var item in data.spawnPoints)
                        NALQueue.Enqueue(new Pair<Vector2, Pair<byte, byte>>(item.Key, item.Value));
                    List<AEntity> listEntity = data.entitiesInChunk;
                    for (ushort i = 0; i < listEntity.Count; i++)
                    {
                        AEntity entity = listEntity[i];
                        if (xOffset == 0 || yOffset == 0 || xOffset == chunkCount - 1 || yOffset == chunkCount - 1)
                            entity.transform.position = Extention.GetRandomPointAround((Vector2)entity.transform.position, 2);
                        mobsController.AddMobToUpdate(entity);
                        entity.EnableView();
                    }
                }
            }
        }

        public void BreakUp()
        {
            generator.onSpawnPoint -= AddSpawnPoint;
            generator.onUpdate -= UpdateNALQueue;
            DayCycle.newDay -= UpdateNAL;
            ClearNALQueue();
            NALQueueUpdate.Clear();
        }

        private void OnDisable()
        {
            _cts.Cancel();
            mapData.Clear();
        }
    }

    public class ChunkEntityData
    {
        public Dictionary<Vector2, Pair<byte, byte>> spawnPoints;
        public List<AEntity> entitiesInChunk;
        public ChunkEntityData()
        {
            spawnPoints = new Dictionary<Vector2, Pair<byte, byte>>();
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
        public byte Chance;
        public EntityInfo info;
    }
}