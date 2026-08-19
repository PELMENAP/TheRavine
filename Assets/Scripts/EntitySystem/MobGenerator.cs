using Cysharp.Threading.Tasks;

using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

using TheRavine.Generator;
using TheRavine.Extensions;

namespace TheRavine.EntityControl
{
    public class MobGenerator : MonoBehaviour, ISetAble
    {
        private const int chunkScale = MapGenerator.chunkScale;
        private const int PruneMarginChunks = 2;

        [SerializeField] private SpawnPointDataHeight[] regions;
        [SerializeField, Min(0)] private int MaxSpawnEntityCount;
        [SerializeField] private int step;

        private MapGenerator mapGenerator;
        private MobController mobController;
        private MobNAL _nalSystem;

        private readonly Dictionary<Vector2Int, ChunkEntityData> mapData = new(64);
        private readonly Dictionary<AEntity, Vector2Int> entityChunkLookup = new(256);

        private readonly HashSet<Vector2Int> _diffBuf = new(64);
        private readonly HashSet<Vector2Int> _newChunksBuf = new(64);
        private List<Vector2Int> _pruneBuf;

        private Vector2Int oldChunkPosition;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);

            mapGenerator = ServiceLocator.GetService<MapGenerator>();
            mobController = ServiceLocator.GetService<MobController>();
            if (mapGenerator != null)
            {
                mapGenerator.chunkGenerator.onSpawnPoint += AddSpawnPoint;
                mapGenerator.onUpdate += UpdateChunks;
            }

            _nalSystem = new MobNAL(regions, step, mobController, MaxSpawnEntityCount);
            _nalSystem.StartNALProcess().Forget();
            _nalSystem.RunLifecycle(ServiceLocator.GetService<EntitySystem>(), RegisterSpawnedEntity).Forget();

            callback?.Invoke();
        }

        private ChunkEntityData GetOrCreateChunkData(Vector2Int pos)
        {
            if (!mapData.TryGetValue(pos, out var data))
            {
                data = new ChunkEntityData();
                mapData[pos] = data;
            }
            return data;
        }

        private void AddSpawnPoint(Vector2Int position, int height, int temperature, Vector2Int chunkCenter)
        {
            GetOrCreateChunkData(chunkCenter).spawnPoints[position] = new Pair<int, int>(height, temperature);
        }

        private void RegisterSpawnedEntity(AEntity entity, GameObject entityObject, Vector2Int worldPos)
        {
            long packed = Position2Int.Pack(worldPos.x, worldPos.y);
            long chunkKey = mapGenerator.GetPosition2Int(packed);
            Vector2Int chunkPos = Position2Int.UnpackToVector(chunkKey);

            if (!entity.HasComponent<VisualCullingComponent>())
            {
                var view = entityObject.GetComponentInChildren<EntityView>(true);
                entity.AddComponentToEntity(new VisualCullingComponent(entityObject, view != null ? view.LabelObject : null));
            }

            GetOrCreateChunkData(chunkPos).entitiesInChunk.Add(entity);
            entityChunkLookup[entity] = chunkPos;

            var mortality = entity.GetEntityComponent<MortalityComponent>();
            if (mortality != null)
                mortality.Died += () => HandleEntityDied(entity);
        }

        private void HandleEntityDied(AEntity entity)
        {
            if (!entityChunkLookup.TryGetValue(entity, out var chunkPos)) return;
            entityChunkLookup.Remove(entity);

            if (mapData.TryGetValue(chunkPos, out var data))
                data.entitiesInChunk.Remove(entity);

            mobController.RemoveMobFromUpdate(entity);
        }

        private void CollectDifference(Vector2Int oldChunk, Vector2Int newChunk, HashSet<Vector2Int> result)
        {
            result.Clear();
            _newChunksBuf.Clear();

            for (int y = -chunkScale; y <= chunkScale; y++)
                for (int x = -chunkScale; x <= chunkScale; x++)
                    _newChunksBuf.Add(newChunk + new Vector2Int(x, y));

            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    Vector2Int c = oldChunk + new Vector2Int(x, y);
                    if (!_newChunksBuf.Contains(c))
                        result.Add(c);
                }
            }
        }

        private void UpdateChunks(long _position)
        {
            Vector2Int position = Position2Int.UnpackToVector(_position);

            CollectDifference(oldChunkPosition, position, _diffBuf);

            foreach (var chunkPos in _diffBuf)
            {
                if (!mapData.TryGetValue(chunkPos, out var data)) continue;

                var entities = data.entitiesInChunk;
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    entity.Deactivate();
                    mobController.RemoveMobFromUpdate(entity);
                    entity.GetEntityComponent<VisualCullingComponent>()?.SetVisible(false);
                }
            }

            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    Vector2Int chunkPos = position + new Vector2Int(x, y);
                    if (!mapData.TryGetValue(chunkPos, out var data)) continue;

                    var entities = data.entitiesInChunk;
                    for (int i = 0; i < entities.Count; i++)
                    {
                        var entity = entities[i];
                        mobController.AddMobToUpdate(entity);
                        entity.Activate();
                        entity.GetEntityComponent<VisualCullingComponent>()?.SetVisible(true);
                    }
                }
            }

            PruneStaleChunks(position);

            oldChunkPosition = position;

            UpdateNALQueue(position);
        }

        private void PruneStaleChunks(Vector2Int center)
        {
            int radius = chunkScale + PruneMarginChunks;

            _pruneBuf?.Clear();

            foreach (var kv in mapData)
            {
                if (kv.Value.entitiesInChunk.Count > 0) continue;

                Vector2Int d = kv.Key - center;
                if (Mathf.Abs(d.x) <= radius && Mathf.Abs(d.y) <= radius) continue;

                (_pruneBuf ??= new List<Vector2Int>(8)).Add(kv.Key);
            }

            if (_pruneBuf == null) return;
            for (int i = 0; i < _pruneBuf.Count; i++)
                mapData.Remove(_pruneBuf[i]);
        }

        private void UpdateNALQueue(Vector2Int centerPos)
        {
            _nalSystem.ClearQueues();

            for (int y = -chunkScale; y <= chunkScale; y++)
            {
                for (int x = -chunkScale; x <= chunkScale; x++)
                {
                    Vector2Int chunkPos = centerPos + new Vector2Int(x, y);
                    if (!mapData.TryGetValue(chunkPos, out var data)) continue;

                    foreach (var spawnPoint in data.spawnPoints)
                        _nalSystem.AddSpawnPointToQueue(spawnPoint.Key, spawnPoint.Value);
                }
            }
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            if (mapGenerator != null)
            {
                mapGenerator.chunkGenerator.onSpawnPoint -= AddSpawnPoint;
                mapGenerator.onUpdate -= UpdateChunks;
            }
            callback?.Invoke();
        }

        private void OnDisable()
        {
            mapData.Clear();
            entityChunkLookup.Clear();
            _nalSystem?.Dispose();
        }
    }

    public class ChunkEntityData
    {
        public readonly Dictionary<Vector2Int, Pair<int, int>> spawnPoints = new();
        public readonly List<AEntity> entitiesInChunk = new();
    }

    [Serializable]
    public struct SpawnPointDataHeight
    {
        public SpawnPointDataTemperatureLevel[] temperatureLevels;
    }

    [Serializable]
    public struct SpawnPointDataTemperatureLevel
    {
        public MobSpawnData[] entities;
    }

    [Serializable]
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
            await UniTask.Delay(5000, cancellationToken: _cts.Token);

            int countCycle = 0;

            while (!_cts.IsCancellationRequested)
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

        public async UniTaskVoid RunLifecycle(EntitySystem entitySystem, Action<AEntity, GameObject, Vector2Int> onSpawned)
        {
            while (!_cts.IsCancellationRequested)
            {
                await UniTask.Delay(10000, cancellationToken: _cts.Token);

                while (_nalSpawnQueue.Count > 0 && _mobController.GetEntityCount() < _maxSpawnEntityCount)
                {
                    Pair<Vector2Int, GameObject> item = _nalSpawnQueue.Dequeue();

                    GameObject curMob = entitySystem.CreateMob(Extension.GetRandomPointAround(item.First, 2), item.Second);
                    if (curMob == null) continue;

                    var viewModel = curMob.GetComponent<AEntityViewModel>();
                    if (viewModel?.Entity != null)
                        onSpawned?.Invoke(viewModel.Entity, curMob, item.First);
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}