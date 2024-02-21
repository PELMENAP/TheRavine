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
        private const byte chunkCount = MapGenerator.chunkCount;
        private GameObject CreateMob(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        //[SerializeField] private int count;
        [SerializeField] private EntityInfo _playerInfo;
        [SerializeField] private SpawnPointData[] regions;
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
            player.SetUpEntityData(_playerInfo);
            entitySystem.AddToGlobal(player);

            NAL().Forget();

            callback?.Invoke();
        }

        private Queue<Pair<Vector2, byte>> NALQueue = new Queue<Pair<Vector2, byte>>(8);
        private Queue<Pair<Vector2, GameObject>> NALQueueUpdate = new Queue<Pair<Vector2, GameObject>>(8);
        public void ClearNALQueue() => NALQueue.Clear();
        [SerializeField] private byte step;
        private async UniTaskVoid NAL()
        {
            await UniTask.Delay(5000);
            bool NALthread = true;
            int countCycle = 0;
            while (NALthread)
            {
                countCycle++;
                if (NALQueue.Count == 0)
                {
                    await UniTask.Delay(1000);
                    print("nobody here");
                    continue;
                }
                else if (countCycle % step == 0)
                {
                    // NALQueue.Dequeue();
                    await UniTask.Delay(100);
                    continue;
                }
                Pair<Vector2, byte> current = NALQueue.Dequeue();
                SpawnPointData currentSpawnPointData = regions[current.Second];
                for (byte i = 0; i < currentSpawnPointData.entities.Length; i++)
                {
                    MobSpawnData curMobSpawnData = currentSpawnPointData.entities[i];
                    if (Random.Range(0, 100) < curMobSpawnData.Chance)
                    {
                        print("summon somebody");
                        NALQueueUpdate.Enqueue(new Pair<Vector2, GameObject>(current.First, curMobSpawnData.info.prefab));
                        await UniTask.Delay(curMobSpawnData.Chance * curMobSpawnData.Chance);
                        break;
                    }
                }
                NALQueue.Enqueue(current);
                await UniTask.Delay(10);
            }
        }
        public void UpdateNAL()
        {
            ClearNALQueue();
            while (NALQueueUpdate.Count > 0)
            {
                Pair<Vector2, GameObject> item = NALQueueUpdate.Dequeue();
                GameObject curMob = CreateMob(item.First, item.Second);
                AEntity entity = curMob.GetComponent<AEntity>();
                entity.transform.position += new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), 0);
                entity.SetUpEntityData(entitySystem.GetMobInfo(item.Second.GetInstanceID()));
                entity.Init();
                GetMapData(generator.GetChunkPosition(item.First)).entitiesInChunk.Add(entity);
            }
            UpdateNALQueue(currentChunkPosition);
        }
        private void AddSpawnPoint(Vector2 position, byte height, Vector2 chunkCenter)
        {
            GetMapData(chunkCenter).spawnPoints[position] = height;
        }

        private List<AEntity> mobEntities = new List<AEntity>();
        private void UpdateNALQueue(Vector2 position)
        {
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    if (xOffset == chunkCount / 2 && yOffset == chunkCount / 2)
                        continue;
                    Vector2 pos = currentChunkPosition + new Vector2(xOffset, yOffset);
                    if (mapData.ContainsKey(pos))
                    {
                        List<AEntity> listEntity = mapData[pos].entitiesInChunk;
                        for (ushort i = 0; i < listEntity.Count; i++)
                        {
                            GetMapData(generator.GetChunkPosition(listEntity[i].GetEntityPosition())).entitiesInChunk.Add(listEntity[i]);
                            listEntity[i].transform.position += new Vector3(Random.Range(-20, 20), Random.Range(-20, 20), 0);
                            listEntity[i].DisableView();
                            mapData[pos].entitiesInChunk.Remove(listEntity[i]);
                        }
                    }
                }
            }
            currentChunkPosition = position;
            mobEntities.Clear();
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    if (xOffset == chunkCount / 2 && yOffset == chunkCount / 2)
                        continue;
                    Vector2 pos = currentChunkPosition + new Vector2(xOffset, yOffset);
                    if (mapData.ContainsKey(pos))
                    {
                        foreach (var item in mapData[pos].spawnPoints)
                            NALQueue.Enqueue(new Pair<Vector2, byte>(item.Key, item.Value));
                        List<AEntity> listEntity = mapData[pos].entitiesInChunk;
                        for (ushort i = 0; i < listEntity.Count; i++)
                        {
                            mobEntities.Add(listEntity[i]);
                            listEntity[i].EnableView();
                        }
                    }
                }
            }
            mobsController.UpdateCurrentMobs(mobEntities);

            print("update NUL queue and mobcontroller");
        }

        public void BreakUp()
        {
            mapData.Clear();
            NALQueue.Clear();
            generator.onSpawnPoint -= AddSpawnPoint;
            generator.onUpdate -= UpdateNALQueue;
            DayCycle.newDay -= UpdateNAL;
        }
    }

    public class ChunkEntityData
    {
        public Dictionary<Vector2, byte> spawnPoints;
        public List<AEntity> entitiesInChunk;
        public ChunkEntityData()
        {
            spawnPoints = new Dictionary<Vector2, byte>();
            entitiesInChunk = new List<AEntity>();
        }
    }

    [System.Serializable]
    public struct SpawnPointData
    {
        public MobSpawnData[] entities;
    }

    [System.Serializable]
    public struct MobSpawnData
    {
        public byte Chance;
        public EntityInfo info;
    }
}