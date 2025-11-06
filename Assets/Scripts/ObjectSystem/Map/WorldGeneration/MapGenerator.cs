using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    using System;
    using EndlessGenerators;
    public class MapGenerator : MonoBehaviour, ISetAble
    {
        private readonly CancellationTokenSource _cts = new();
        public const int mapChunkSize = 16, chunkScale = 1, scale = 5, generationSize = scale * mapChunkSize;
        private Dictionary<Vector2Int, ChunkData> mapData;
        public ChunkData GetMapData(Vector2Int position)
        {
            if (mapData.TryGetValue(position, out ChunkData data))
                return data;
            mapData[position] = GenerateMapData(position);
            return mapData[position];
        }

        public bool IsHeightIsLiveAble(int height) => regions[height].liveAble;
        public bool IsWaterHeight(Vector2Int position) => GetMapHeight(position) < 1;

        public int GetMapHeight(Vector2Int position)
        {
            Vector2Int playerPos = new(position.x, position.y);
            Vector2Int chunkPos = GetChunkPosition(playerPos);
            Vector2Int XYpos = (playerPos - chunkPos * generationSize) / scale;
            if (XYpos.x > mapChunkSize - 1)
                XYpos.x = mapChunkSize - 1;
            if (XYpos.y > mapChunkSize - 1)
                XYpos.y = mapChunkSize - 1;
            if (XYpos.x < 0)
                XYpos.x = 0;
            if (XYpos.y < 0)
                XYpos.y = 0;
            return GetMapData(chunkPos).heightMap[XYpos.x, XYpos.y];
        }

        public Vector2Int GetChunkPosition(Vector2 position)
        {
            Vector2Int positionInt = new((int)position.x, (int)position.y);
            Vector2Int chunkPos = positionInt / generationSize;
            if (positionInt.x < 0)
                chunkPos.x = -(-positionInt.x / generationSize) - 1;
            if (positionInt.y < 0)
                chunkPos.y = -(-positionInt.y / generationSize) - 1;
            return new Vector2Int(chunkPos.x, chunkPos.y);
        }

        public ChunkData GetMapDataByObjectPosition(Vector2Int position)
        {
            Vector2Int chunkPos = GetChunkPosition(position);
            if (!mapData.ContainsKey(chunkPos)) return null;
            return mapData[chunkPos];
        }

        public bool TryToAddPositionToChunk(Vector2Int position)
        {
            SortedSet<Vector2Int> objectsToInst = GetMapData(GetChunkPosition(position)).objectsToInst;
            if (objectsToInst.Contains(position))
                return false;
            else
            {
                objectsToInst.Add(position);
                return true;
            }
        }
        public Transform terrainTransform, waterTransform;
        public MeshFilter terrainFilter;
        public MeshCollider terrainCollider; 
        private int seed;
        public int Seed { get => seed; private set => seed = value; }
        private ObjectSystem objectSystem;
        [SerializeField] private float noiseScale;
        [SerializeField] private int octaves;
        [SerializeField, Range(0, 1)] private float persistence;
        [SerializeField] private float lacunarity;
        [SerializeField] private TerrainType[] regions;
        [SerializeField] private TemperatureType[] biomRegions;
        [SerializeField] private Transform viewer;
        [SerializeField] private bool[] endlessFlag;
        [SerializeField] private bool isRiver;
        private IEndless[] endless;
        public void SetUp(ISetAble.Callback callback)
        {
            // seed = RavineRandom.RangeInt(0, 1000);
            seed = 16; 
            endless = new IEndless[3];
            mapData = new Dictionary<Vector2Int, ChunkData>(64);
            Noise.SetInit(noiseScale, octaves, persistence, lacunarity, Seed);
            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            if (endlessFlag[3])
            {
                NAL().Forget();
                UpdateNAL().Forget();
            }
            if (endlessFlag[0])
                endless[0] = new EndlessTerrain(this);
            if (endlessFlag[1])
                endless[1] = new EndlessLiquids(this);
            if (endlessFlag[2])
                endless[2] = new EndlessObjects(this, objectSystem);
            FirstInstance().Forget();
            callback?.Invoke();
        }

        private async UniTaskVoid FirstInstance()
        {
            for (int i = -scale; i < scale; i++)
            {
                for (int j = -scale; j < scale; j++)
                {
                    Vector2Int centre = new(i, j);
                    mapData[centre] = GenerateMapData(centre);
                    await UniTask.Delay(50);
                }
            }
            viewer = ServiceLocator.Players.GetFirstPlayer();
            GenerationUpdate().Forget();
        }

        private Queue<Vector2Int> NALQueue, NALQueueUpdateRemove;
        private Queue<Pair<Vector2Int,  ObjectInfo>> NALQueueUpdateAdd;
        public void ClearNALQueue() => NALQueue.Clear();
        public void AddNALObject(Vector2Int current) => NALQueue.Enqueue(current);

        private readonly int step;
        private int deadChance = 0;

        private async UniTaskVoid NAL()
        {
            NALQueue = new Queue<Vector2Int>(64);
            NALQueueUpdateRemove = new Queue<Vector2Int>(32);
            NALQueueUpdateAdd = new Queue<Pair<Vector2Int,  ObjectInfo>>(32);
            await UniTask.Delay(10000);
            int countCycle = 0;
            while (!_cts.Token.IsCancellationRequested)
            {
                countCycle++;
                if (NALQueue.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: _cts.Token);
                    continue;
                }
                if (NALQueue.Count > 100)
                {
                    if (countCycle % (step * step) != 0)
                    {
                        NALQueue.Dequeue();
                        await UniTask.Delay(100, cancellationToken: _cts.Token);
                        continue;
                    }
                }
                else if (countCycle % step != 0)
                {
                    NALQueue.Dequeue();
                    await UniTask.Delay(100, cancellationToken: _cts.Token);
                    continue;
                }
                deadChance = NALQueue.Count > 200 ? 10 : 0;
                Vector2Int current = NALQueue.Dequeue();
                ObjectInfo currentObjectPrefabData = objectSystem.GetGlobalObjectInfo(current);
                if(currentObjectPrefabData == null) continue;
                ObjectInfo nextGenInfo = currentObjectPrefabData.EvolutionStep;
                if (currentObjectPrefabData.BehaviourType == BehaviourType.GROW)
                {
                    if (nextGenInfo == null)
                    {
                        if (Extension.ComparePercent(25))
                            NALQueueUpdateRemove.Enqueue(current);
                        continue;
                    }
                    NALQueueUpdateRemove.Enqueue(current);
                    NALQueueUpdateAdd.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, nextGenInfo));
                    continue;
                }
                // ObjectInfo childObjectInfo = objectSystem.GetPrefabInfo(currentObjectInfo.childPrefab.GetInstanceID());
                bool closeto = false;
                for (int x = -scale; x <= scale; x++)
                    for (int y = -scale; y <= scale; y++)
                        if (x != 0 && y != 0 && objectSystem.ContainsGlobal(current + new Vector2Int(x, y)))
                            closeto = true;
                NAlInfo nalinfo = currentObjectPrefabData.NalInfo;
                if (Extension.ComparePercent(nalinfo.chance / 2 + deadChance) || closeto)
                {
                    NALQueueUpdateRemove.Enqueue(current);
                    SpreadPattern pattern = currentObjectPrefabData.OnDeathPattern;
                    if (pattern == null)
                        continue;
                    NALQueueUpdateAdd.Enqueue(new Pair<Vector2Int, ObjectInfo>(current, pattern.main));
                    if (pattern.other.Length != 0)
                    {
                        for (int i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2Int newPos = Extension.GetRandomPointAround(current, pattern.factor);
                            NALQueueUpdateAdd.Enqueue(new Pair<Vector2Int, ObjectInfo>(newPos, pattern.other[i]));
                        }
                    }
                    continue;
                }
                else
                {
                    if (!IsHeightIsLiveAble(GetMapHeight(current)))
                        continue;
                    int attempts = nalinfo.attempt;
                    while (attempts > 0)
                    {
                        if (Extension.ComparePercent(nalinfo.chance))
                        {
                            Vector2Int newPos = Extension.GetRandomPointAround(current, nalinfo.distance);
                            NALQueueUpdateAdd.Enqueue(new Pair<Vector2Int, ObjectInfo>(newPos, nextGenInfo));
                        }
                        await UniTask.Delay(10, cancellationToken: _cts.Token);
                        attempts--;
                    }
                }
                await UniTask.Delay(10 * nalinfo.delay, cancellationToken: _cts.Token); // nalinfo * 100
            }
        }

        public async UniTaskVoid UpdateNAL()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await UniTask.Delay(100000);
                if (rotateTarget != 0f)
                    return;
                ClearNALQueue();
                while (NALQueueUpdateRemove.Count > 0)
                {   
                    Vector2Int item = NALQueueUpdateRemove.Dequeue();
                    objectSystem.RemoveFromGlobal(item);
                }
                while (NALQueueUpdateAdd.Count > 0)
                {
                    Pair<Vector2Int, ObjectInfo> item = NALQueueUpdateAdd.Dequeue();
                    if(objectSystem.TryAddToGlobal(item.First, item.Second.PrefabID, item.Second.DefaultAmount, item.Second.InstanceType, (item.First.x + item.First.y) % 2 == 0))
                        GetMapData(GetChunkPosition(item.First)).objectsToInst.Add(item.First);
                }
                ExtraUpdate();
            }
        }
        private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
        private float[,] noiseTemperatureMap = new float[mapChunkSize, mapChunkSize];
        public UnityAction<Vector2Int, int, int, Vector2Int> onSpawnPoint;
        private readonly int[] countOfHeights = new int[9];
        private int count = 0;
        [SerializeField] private readonly float riverMin = 0.4f,  riverMax = 0.6f, riverInfluence = 0.05f, maxRiverDepth = 0.3f;
        public ChunkData GenerateMapData(Vector2Int centre)
        {
            FastRandom chunkRandom = new((seed + centre.x + centre.y));
            SortedSet<Vector2Int> objectsToInst = new(new Vector2IntComparer());
            int[,] heightMap = new int[mapChunkSize, mapChunkSize];
            int[,] temperatureMap = new int[mapChunkSize, mapChunkSize];


            Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Global);
            Noise.GenerateNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize, Noise.NormalizeMode.Global, true);

            if(isRiver)
                Noise.CombineMaps(ref noiseMap, noiseTemperatureMap, riverMin, riverMax, riverInfluence, maxRiverDepth);

            // if (Mathf.Abs(centre.x) > 10000 || Mathf.Abs(centre.y) > 10000)
            //     Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Local);
            // else
            //     Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Global);

            // Noise.GenerateTempNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize);

            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    float currentHeight = noiseMap[x, y];
                    for (int i = 0; i < regions.Length; i++)
                    {
                        if (currentHeight >= regions[i].height)
                        {
                            heightMap[x, y] = i;
                        }
                        else
                            break;
                    }
                    currentHeight = noiseTemperatureMap[x, y];
                    if(heightMap[x,y] == 8)
                    {
                        temperatureMap[x,y] = 0;
                        continue;
                    }
                    for (int i = 0; i + 1 < biomRegions.Length; i++)
                    {
                        if (!(currentHeight >= biomRegions[i + 1].height))
                        {
                            temperatureMap[x, y] = i;
                            break;
                        }
                    }
                }
            }
            for (int x = 0; x < mapChunkSize; x++)
            {
                for (int y = 0; y < mapChunkSize; y++)
                {
                    if (!endlessFlag[2])
                        continue;
                    countOfHeights[heightMap[x, y]]++;
                    bool structHere = false;
                    TemperatureLevel level = regions[heightMap[x, y]].level[temperatureMap[x, y]];
                    // for (int i = 0; i < level.structs.Length; i++)
                    // {
                    //     StructInfoGeneration sinfo = level.structs[i];
                    //     if(sinfo.Chance == 0)
                    //         continue;
                    //     if ((x * y + centre.x * centre.y + Seed + i * countOfHeights[heightMap[x, y]] + count) % sinfo.Chance == 0)
                    //     {
                    //         Vector2Int posstruct = new(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale);
                    //         var WFCAobjects = WFCA(posstruct, (int)((Seed + (int)x + (int)y) % sinfo.info.tileInfo.Length), sinfo.info);
                    //         foreach (var item in WFCAobjects)
                    //         {
                    //             if (objectSystem.TryAddToGlobal(item.Key, item.Value.prefabID, item.Value.amount, item.Value.iType, (x + y) % 2 == 0))
                    //                 {
                    //                     var data = GetMapDataByObjectPosition(item.Key);
                    //                     objectsToInst.Add(item.Key);
                    //                 }
                    //         }
                    //         structHere = true;
                    //         if (sinfo.isSpawnPoint)
                    //             onSpawnPoint?.Invoke(posstruct, heightMap[x, y], temperatureMap[x, y], centre);
                    //         break;
                    //     }
                    // }
                    if (structHere) continue;
                    for (int i = 0; i < level.objects.Length; i++)
                    {
                        ObjectInfoGeneration ginfo = level.objects[i];
                        if(ginfo.Chance == 0 || ginfo.info == null) continue;
                        if (chunkRandom.Range(0, 1000) < ginfo.Chance)
                        {
                            Vector2Int posobj = new(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale);
                           
                                if (objectSystem.TryAddToGlobal(posobj, ginfo.info.PrefabID, ginfo.info.DefaultAmount, ginfo.info.InstanceType, (x + y) % 2 == 0))
                                {
                                    objectsToInst.Add(posobj);
                                    break;
                                }
                        }
                    }
                    count += x + y;
                }
            }
            return new ChunkData(heightMap, temperatureMap, objectsToInst);
        }

        private Vector2Int OldVposition, position;
        public UnityAction<Vector2Int> onUpdate;
        private async UniTaskVoid GenerationUpdate()
        {
            position = GetPlayerPosition();
            for (int i = 0; i < 3; i++)
            {
                if(!endlessFlag[i]) continue;
                endless[i].UpdateChunk(position);
                await UniTask.WaitForFixedUpdate();
            }
            while (!_cts.Token.IsCancellationRequested)
            {
                position = GetPlayerPosition();
                Debug.Log(GetMapHeight(position));
                if (position != OldVposition && rotateTarget == 0f)
                {
                    OldVposition = position;
                    int extendedChunk = chunkScale + 1;
                    for (int yOffset = -extendedChunk; yOffset <= extendedChunk; yOffset++)
                    {
                        for (int xOffset = -extendedChunk; xOffset <= extendedChunk; xOffset++)
                        {
                            Vector2Int chunkPosition = new(position.x + xOffset, position.y + yOffset);
                            if(!mapData.ContainsKey(chunkPosition)) mapData[chunkPosition] = GenerateMapData(chunkPosition);
                        }
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        if(!endlessFlag[i]) continue;
                        endless[i].UpdateChunk(position);
                        await UniTask.WaitForFixedUpdate();
                    }
                    onUpdate?.Invoke(position);
                }
                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }
        public void ExtraUpdate()
        {
            if (rotateTarget != 0f)
                return;
            position = GetPlayerPosition();
            if(!endlessFlag[2]) return;
            endless[2].UpdateChunk(position);
        }

        private Vector2Int GetPlayerPosition()
        {
            if (viewer == null) return Vector2Int.zero;
            Vector3 playerPosition = new(viewer.position.x - generationSize / 2, viewer.position.z + generationSize / 2, 0);
            return Extension.RoundVector2D(playerPosition / generationSize);
        }
        public float rotateValue = 0f, rotateTarget = 0f;

        public void BreakUp(ISetAble.Callback callback)
        {
            NALQueue.Clear();
            NALQueueUpdateAdd.Clear();
            NALQueueUpdateRemove.Clear();
            mapData.Clear();
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            _cts.Cancel();
        }
    }

    [Serializable]
    public struct TerrainType
    {
        public float height;
        public bool liveAble;
        public TemperatureLevel[] level;
    }
    [Serializable]
    public struct TemperatureLevel
    {
        public ObjectInfoGeneration[] objects;
        public StructInfoGeneration[] structs;
    }

    [Serializable]
    public struct ObjectInfoGeneration
    {
        public int Chance;
        public ObjectInfo info;
    }

    [Serializable]
    public struct StructInfoGeneration
    {
        public bool isSpawnPoint;
        public int Chance;
        // public StructInfo info;
    }

    [Serializable]
    public struct TemperatureType
    {
        public float height;
    }
    public class ChunkData
    {
        public readonly int[,] heightMap, temperatureMap;
        public SortedSet<Vector2Int> objectsToInst;
        public ChunkData(int[,] heightMap, int[,] temperatureMap, SortedSet<Vector2Int> objectsToInst)
        {
            this.heightMap = heightMap;
            this.temperatureMap = temperatureMap;
            this.objectsToInst = objectsToInst;
        }
    }

}