using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extentions;
using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class MobGenerator : MonoBehaviour, ISetAble
    {
        private const byte chunkCount = MapGenerator.chunkCount;
        private GameObject CreateMob(Vector2 position, GameObject prefab) => Instantiate(prefab, position, Quaternion.identity);
        //[SerializeField] private int count;
        [SerializeField] private EntityInfo _playerInfo;
        [SerializeField] private SpawnPointData[] regions;
        private IEntity player;
        private MapGenerator generator;
        private EntitySystem entitySystem;
        private Dictionary<Vector2, ChunkEntityData> mapData = new Dictionary<Vector2, ChunkEntityData>(4);
        private Vector2 currentChunkPosition, oldChunkPosition;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            generator = locator.GetService<MapGenerator>();
            generator.onSpawnPoint += AddSpawnPoint;
            generator.onUpdate += UpdateNALQueue;

            player = locator.GetService<PlayerEntity>();
            entitySystem = locator.GetService<EntitySystem>();
            player.SetUpEntityData(_playerInfo);
            entitySystem.AddToGlobal(player);

            NAL().Forget();

            callback?.Invoke();
        }

        private Queue<Pair<Vector2, byte>> NALQueue = new Queue<Pair<Vector2, byte>>(8);
        [SerializeField] private byte step;
        private async UniTaskVoid NAL()
        {
            await UniTask.Delay(10000);
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
                print(current.First);
                SpawnPointData currentSpawnPointData = regions[current.Second];
                for (byte i = 0; i < currentSpawnPointData.entities.Length; i++)
                {
                    print("iterate entities");
                    MobSpawnData curMobSpawnData = currentSpawnPointData.entities[i];
                    if (Random.Range(0, 100) < curMobSpawnData.Chance)
                    {
                        print("summon somebody");
                        GameObject curMob = CreateMob(current.First, curMobSpawnData.info.prefab);
                        IEntity entity = curMob.GetComponent<IEntity>();
                        entity.SetUpEntityData(entitySystem.GetMobInfo(curMobSpawnData.info.prefab.GetInstanceID()));
                        mapData[generator.GetChunkPosition(current.First)].entitiesInChunk.Add(entity);
                        await UniTask.Delay(curMobSpawnData.Chance * curMobSpawnData.Chance);
                        break;
                    }
                }
                NALQueue.Enqueue(current);
                await UniTask.Delay(10);
            }
        }

        private void AddSpawnPoint(Vector2 position, byte height, Vector2 chunkCenter)
        {
            if (!mapData.ContainsKey(chunkCenter))
                mapData[chunkCenter] = new ChunkEntityData();
            if (!mapData[chunkCenter].spawnPoints.ContainsKey(position))
                mapData[chunkCenter].spawnPoints[position] = height;
            print("add spawn point");
        }

        private void UpdateNALQueue(Vector2 position)
        {
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    Vector2 pos = currentChunkPosition + new Vector2(xOffset, yOffset);
                    if (mapData.ContainsKey(pos))
                    {
                        foreach (IEntity item in mapData[pos].entitiesInChunk)
                        {
                            // проверить позиции и передать их новым чанкам
                            mapData[generator.GetChunkPosition(item.GetEntityPosition())].entitiesInChunk.Add(item);
                            item.DisableView();
                            mapData[pos].entitiesInChunk.Remove(item);
                        }
                    }
                }
            }
            currentChunkPosition = position;
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    Vector2 pos = position + new Vector2(xOffset, yOffset);
                    if (mapData.ContainsKey(pos))
                    {
                        foreach (var item in mapData[pos].spawnPoints)
                            NALQueue.Enqueue(new Pair<Vector2, byte>(item.Key, item.Value));
                        foreach (IEntity item in mapData[pos].entitiesInChunk)
                            item.EnableVeiw();
                    }
                }
            }
            print("update NUL queue");
        }

        private void FixedUpdate()
        {
            for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
            {
                for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
                {
                    Vector2 pos = currentChunkPosition + new Vector2(xOffset, yOffset);
                    if (mapData.ContainsKey(pos))
                        foreach (IEntity item in mapData[pos].entitiesInChunk)
                            item.UpdateEntityCycle();
                }
            }
        }

        public void BreakUp()
        {
            generator.onSpawnPoint -= AddSpawnPoint;
            generator.onUpdate -= UpdateNALQueue;
        }
    }

    public class ChunkEntityData
    {
        public Dictionary<Vector2, byte> spawnPoints;
        public SortedSet<IEntity> entitiesInChunk;
        public ChunkEntityData()
        {
            spawnPoints = new Dictionary<Vector2, byte>();
            entitiesInChunk = new SortedSet<IEntity>();
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