using Cysharp.Threading.Tasks;

using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extensions;

namespace TheRavine.EntityControl
{
    public class MobGenerator : MonoBehaviour, ISetAble // implement SetUp like Start and BreakDown like OnDestroy
    {
        private const int chunkScale = MapGenerator.chunkScale;
        [SerializeField] private SpawnPointDataHeight[] regions;
        [SerializeField, Min(0)] private int MaxSpawnEntityCount;
        private AEntity player;
        private MapGenerator mapGenerator;
        private MobController mobController;
        private MobNAL _nalSystem; // natural artificial life
        [SerializeField] private int step;
        private readonly Dictionary<Vector2Int, ChunkEntityData> mapData = new(4);
        
        private ChunkEntityData GetMapData(Vector2Int pos)
        {
            if (!mapData.ContainsKey(pos))
                mapData[pos] = new ChunkEntityData();
            return mapData[pos];
        }
        private Vector2Int currentChunkPosition, oldChunkPosition;
        public void SetUp(ISetAble.Callback callback)
        {
            mapGenerator = ServiceLocator.GetService<MapGenerator>();
            mobController = ServiceLocator.GetService<MobController>();
            if(mapGenerator != null)
            {
                mapGenerator.chunkGenerator.onSpawnPoint += AddSpawnPoint;
                mapGenerator.onUpdate += UpdateChunks;
            }

            _nalSystem = new MobNAL(
                regions, 
                step,
                mobController, 
                MaxSpawnEntityCount
            );
            _nalSystem.StartNALProcess().Forget();
            _nalSystem.RunLifecycle(ServiceLocator.GetService<EntitySystem>()).Forget();

            callback?.Invoke();
        }
        private void AddSpawnPoint(Vector2Int position, int height, int temperature, Vector2Int chunkCenter)
        {
            GetMapData(chunkCenter).spawnPoints[position] = new Pair<int, int>(height, temperature);
        }
        private HashSet<Vector2Int> GetChunksDifference(Vector2Int oldChunk, Vector2Int newChunk)
        {
            HashSet<Vector2Int> oldChunks = new();
            HashSet<Vector2Int> newChunks = new();

            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    oldChunks.Add(oldChunk + new Vector2Int(x, y));
                    newChunks.Add(newChunk + new Vector2Int(x, y));
                }
            }

            oldChunks.ExceptWith(newChunks);
            return oldChunks;
        }
        private void UpdateChunks(Vector2Int position)
        {
            HashSet<Vector2Int> chunksToDeactivate = GetChunksDifference(oldChunkPosition, position);
            
            foreach (var chunkPos in chunksToDeactivate)
            {
                if (mapData.TryGetValue(chunkPos, out var data))
                {
                    foreach (var entity in data.entitiesInChunk)
                    {
                        entity.Deactivate();
                        mobController.RemoveMobFromUpdate(entity);
                    }
                }
            }

            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    Vector2Int chunkPos = position + new Vector2Int(x, y);
                    if (mapData.TryGetValue(chunkPos, out var data))
                    {
                        foreach (var entity in data.entitiesInChunk)
                        {
                            mobController.AddMobToUpdate(entity);
                            entity.Activate();
                        }
                    }
                }
            }

            oldChunkPosition = currentChunkPosition;
            currentChunkPosition = position;

            UpdateNALQueue(currentChunkPosition);
        }

        private void UpdateNALQueue(Vector2Int centerPos)
        {
            _nalSystem.ClearQueues();
            
            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    Vector2Int chunkPos = centerPos + new Vector2Int(x, y);
                    ChunkEntityData data = GetMapData(chunkPos);
                    
                    foreach (var spawnPoint in data.spawnPoints)
                    {
                        _nalSystem.AddSpawnPointToQueue(spawnPoint.Key, spawnPoint.Value);
                    }
                }
            }
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            if(mapGenerator != null)
            {
                mapGenerator.chunkGenerator.onSpawnPoint -= AddSpawnPoint;
                mapGenerator.onUpdate -= UpdateChunks;
            }
            callback?.Invoke();
        }

        private void OnDisable()
        {
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


    public class MobNAL : IDisposable
    {
        private readonly Queue<Pair<Vector2Int, Pair<int, int>>> _nalQueue = new(8);
        private readonly Queue<Pair<Vector2Int, GameObject>> _nalSpawnQueue = new(8);
        private CancellationTokenSource _cts = new();
        private readonly SpawnPointDataHeight[] _regions;
        private readonly int _step;
        private readonly MobController _mobController;
        private readonly int _maxSpawnEntityCount;
        
        public MobNAL(SpawnPointDataHeight[] regions, int step, MobController mobController, int maxSpawnEntityCount)
        {
            _regions = regions;
            _step = step;
            _mobController = mobController;
            _maxSpawnEntityCount = maxSpawnEntityCount;
        }

        public void AddSpawnPointToQueue(Vector2Int position, Pair<int, int> heightTempData)
        {
            _nalQueue.Enqueue(new Pair<Vector2Int, Pair<int, int>>(position, heightTempData));
        }

        public void ClearQueues()
        {
            _nalQueue.Clear();
            _nalSpawnQueue.Clear();
        }

        public async UniTaskVoid StartNALProcess()
        {
            await UniTask.Delay(5000);
            
            bool nalActive = true;
            int countCycle = 0;
            
            while (nalActive && !_cts.IsCancellationRequested)
            {
                countCycle++;
                
                if (_nalQueue.Count == 0)
                {
                    await UniTask.Delay(5000, cancellationToken: _cts.Token);
                    continue;
                }
                
                if (countCycle % _step == 0)
                {
                    _nalQueue.Enqueue(_nalQueue.Dequeue());
                    await UniTask.Delay(1000, cancellationToken: _cts.Token);
                    continue;
                }
                
                Pair<Vector2Int, Pair<int, int>> current = _nalQueue.Dequeue();
                MobSpawnData[] currentEntities = _regions[current.Second.First].temperatureLevels[current.Second.Second].entities;
                
                for (int i = 0; i < currentEntities.Length; i++)
                {
                    MobSpawnData curMobSpawnData = currentEntities[i];
                    if (curMobSpawnData.Chance <= 0)
                        continue;
                        
                    if (RavineRandom.Hundred() < curMobSpawnData.Chance)
                    {
                        _nalSpawnQueue.Enqueue(new Pair<Vector2Int, GameObject>(current.First, curMobSpawnData.info.Prefab));
                        await UniTask.Delay(curMobSpawnData.Chance * curMobSpawnData.Chance, cancellationToken: _cts.Token);
                        break;
                    }
                }
                
                _nalQueue.Enqueue(current);
                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }

        public async UniTaskVoid RunLifecycle(EntitySystem entitySystem)
        {
            while (!_cts.IsCancellationRequested)
            {
                await UniTask.Delay(10000);
                
                while (_nalSpawnQueue.Count > 0 && _mobController.GetEntityCount() < _maxSpawnEntityCount)
                {
                    Pair<Vector2Int, GameObject> item = _nalSpawnQueue.Dequeue();

                    GameObject curMob = entitySystem.CreateMob(Extension.GetRandomPointAround(item.First, 2), item.Second);
                    
                    // GetMapData(mapGenerator.GetChunkPosition(item.First)).entitiesInChunk.Add(entity);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}