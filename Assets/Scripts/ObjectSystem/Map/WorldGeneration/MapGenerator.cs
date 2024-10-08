using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.ObjectControl;
using TheRavine.Services;

namespace TheRavine.Generator
{
    using System;
    using EndlessGenerators;
    public class MapGenerator : MonoBehaviour, ISetAble
    {
        private System.Threading.CancellationTokenSource _cts = new();
        public const byte mapChunkSize = 16, chunkScale = 1, scale = 5, generationSize = scale * mapChunkSize, waterLevel = 1;
        private Dictionary<Vector2, ChunkData> mapData;
        public ChunkData GetMapData(Vector2 position)
        {
            if(mapData.TryGetValue(position, out ChunkData data))
                return data;
            return null;
        }
        
        public bool IsHeightIsLiveAble(int height) => regions[height].liveAble;

        public byte GetMapHeight(Vector2 position)
        {
            Vector2 playerPos = new((int)position.x, (int)position.y);
            Vector2 chunkPos = GetChunkPosition(playerPos);
            Vector2 XYpos = (playerPos - chunkPos * generationSize) / scale;
            if (XYpos.x > 15)
                XYpos.x = 15;
            if (XYpos.y > 15)
                XYpos.y = 15;
            if (XYpos.x < 0)
                XYpos.x = 0;
            if (XYpos.y < 0)
                XYpos.y = 0;
            return GetMapData(chunkPos).heightMap[(int)XYpos.x, (int)XYpos.y];
        }

        public Vector2 GetChunkPosition(Vector2 position)
        {
            position = new Vector2(position.x, position.y);
            Vector2 chunkPos = position / generationSize;
            if (position.x < 0)
                chunkPos.x = -(-position.x / generationSize) - 1;
            if (position.y < 0)
                chunkPos.y = -(-position.y / generationSize) - 1;
            return new Vector2((int)chunkPos.x, (int)chunkPos.y);
        }

        public ChunkData GetMapDataByObjectPosition(Vector2 position)
        {
            Vector2 chunkPos = GetChunkPosition(position);
            if (!mapData.ContainsKey(chunkPos)) return null;
            return mapData[chunkPos];
        }

        public bool TryToAddPositionToChunk(Vector2 position)
        {
            SortedSet<Vector2> objectsToInst = GetMapData(GetChunkPosition(position)).objectsToInst;
            if (objectsToInst.Contains(position))
                return false;
            else
            {
                objectsToInst.Add(position);
                return true;
            }
        }
        public Transform terrainT, waterT;
        public MeshFilter terrainF, waterF;
        private int seed;
        public int Seed { get => seed; private set => seed = value; }
        private ObjectSystem objectSystem;
        [SerializeField] private float noiseScale;
        [SerializeField] private byte octaves;
        [SerializeField, Range(0, 1)] private float persistence;
        [SerializeField] private float lacunarity;
        [SerializeField] private TerrainType[] regions;
        [SerializeField] private TemperatureType[] biomRegions;
        [SerializeField] private Transform viewer;
        [SerializeField] private bool[] endlessFlag;
        private IEndless[] endless;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            // seed = RavineRandom.RangeInt(0, 1000);
            seed = 16; 
            endless = new IEndless[3];
            mapData = new Dictionary<Vector2, ChunkData>(64);
            Noise.SetInit(noiseScale, octaves, persistence, lacunarity, Seed);
            // viewer = locator.GetPlayerTransform();
            objectSystem = locator.GetService<ObjectSystem>();
            if (endlessFlag[3])
                NAL().Forget();
            if (endlessFlag[0])
                endless[0] = new EndlessTerrain(this);
            if (endlessFlag[1])
                endless[1] = new EndlessLiquids(this);
            if (endlessFlag[2])
                endless[2] = new EndlessObjects(this, objectSystem);
            UpdateNAL().Forget();
            FirstInstance().Forget();
            callback?.Invoke();
        }

        private async UniTaskVoid FirstInstance()
        {
            for (sbyte i = -scale; i < scale; i++)
            {
                for (sbyte j = -scale; j < scale; j++)
                {
                    Vector2 centre = new(i, j);
                    // Debug.Log(centre);
                    mapData[centre] = GenerateMapData(centre);
                    await UniTask.Delay(10);
                }
            }
            GenerationUpdate().Forget();
        }

        private Queue<Vector2> NALQueue, NALQueueUpdateRemove;
        private Queue<Pair<Vector2,  ObjectInfo>> NALQueueUpdateAdd;
        public void ClearNALQueue() => NALQueue.Clear();
        public void AddNALObject(Vector2 current) => NALQueue.Enqueue(current);
        [SerializeField] private byte step, deadChance = 0;
        private async UniTaskVoid NAL()
        {
            NALQueue = new Queue<Vector2>(64);
            NALQueueUpdateRemove = new Queue<Vector2>(32);
            NALQueueUpdateAdd = new Queue<Pair<Vector2,  ObjectInfo>>(32);
            await UniTask.Delay(10000);
            int countCycle = 0;
            while (!DataStorage.sceneClose)
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
                deadChance = NALQueue.Count > 200 ? (byte)10 : (byte)0;
                Vector2 current = NALQueue.Dequeue();
                ObjectInfo currentObjectPrefabData = objectSystem.GetGlobalObjectInfo(current);
                if(currentObjectPrefabData == null) continue;
                ObjectInfo nextGenInfo = currentObjectPrefabData.nextStep;
                if (currentObjectPrefabData.bType == BehaviourType.GROW)
                {
                    if (nextGenInfo == null)
                    {
                        if (Extension.ComparePercent(25))
                            NALQueueUpdateRemove.Enqueue(current);
                        continue;
                    }
                    NALQueueUpdateRemove.Enqueue(current);
                    NALQueueUpdateAdd.Enqueue(new Pair<Vector2, ObjectInfo>(current, nextGenInfo));
                    continue;
                }
                // ObjectInfo childObjectInfo = objectSystem.GetPrefabInfo(currentObjectInfo.childPrefab.GetInstanceID());
                bool closeto = false;
                for (sbyte x = -scale; x <= scale; x++)
                    for (sbyte y = -scale; y <= scale; y++)
                        if (x != 0 && y != 0 && objectSystem.ContainsGlobal(current + new Vector2(x, y)))
                            closeto = true;
                NAlInfo nalinfo = currentObjectPrefabData.nalinfo;
                if (Extension.ComparePercent(nalinfo.chance / 2 + deadChance) || closeto)
                {
                    NALQueueUpdateRemove.Enqueue(current);
                    SpreadPattern pattern = currentObjectPrefabData.deadPattern;
                    if (pattern == null)
                        continue;
                    NALQueueUpdateAdd.Enqueue(new Pair<Vector2, ObjectInfo>(current, pattern.main));
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2 newPos = Extension.GetRandomPointAround(current, pattern.factor);
                            NALQueueUpdateAdd.Enqueue(new Pair<Vector2, ObjectInfo>(newPos, pattern.other[i]));
                        }
                    }
                    continue;
                }
                else
                {
                    if (!IsHeightIsLiveAble(GetMapHeight(current)))
                        continue;
                    byte attempts = nalinfo.attempt;
                    while (attempts > 0)
                    {
                        if (Extension.ComparePercent(nalinfo.chance))
                        {
                            Vector2 newPos = Extension.GetRandomPointAround(current, nalinfo.distance);
                            NALQueueUpdateAdd.Enqueue(new Pair<Vector2, ObjectInfo>(newPos, nextGenInfo));
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
            while (!DataStorage.sceneClose)
            {
                await UniTask.Delay(100000);
                if (rotateTarget != 0f)
                    return;
                ClearNALQueue();
                while (NALQueueUpdateRemove.Count > 0)
                {   
                    Vector2 item = NALQueueUpdateRemove.Dequeue();
                    objectSystem.RemoveFromGlobal(item);
                }
                while (NALQueueUpdateAdd.Count > 0)
                {
                    Pair<Vector2, ObjectInfo> item = NALQueueUpdateAdd.Dequeue();
                    if(objectSystem.TryAddToGlobal(item.First, item.Second.prefab.GetInstanceID(), item.Second.amount, item.Second.iType, (item.First.x + item.First.y) % 2 == 0))
                        GetMapData(GetChunkPosition(item.First)).objectsToInst.Add(item.First);
                }
                ExtraUpdate();
            }
        }
        private byte[,] GenerateTempMap(Vector2 centre)
        {
            if (mapData.ContainsKey(centre))
                return mapData[centre].temperatureMap;
            byte[,] temperatureMap = new byte[mapChunkSize, mapChunkSize];
            Noise.GenerateTempNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize);
            for (byte x = 0; x < mapChunkSize; x++)
            {
                for (byte y = 0; y < mapChunkSize; y++)
                {
                    float currentHeight = noiseTemperatureMap[x, y];
                    for (byte i = 0; i + 1 < biomRegions.Length; i++)
                    {
                        if (!(currentHeight >= biomRegions[i + 1].height))
                        {
                            temperatureMap[x, y] = i;
                            break;
                        }
                    }
                }
            }
            return temperatureMap;
        }

        private byte GetTempMapPoint(Vector2 centre, int x, int y)
        {
            if (mapData.ContainsKey(centre))
                return mapData[centre].temperatureMap[x, y];
            byte[,] temperatureMap = new byte[mapChunkSize, mapChunkSize];
            Noise.GenerateTempNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize);
            float currentHeight = noiseTemperatureMap[x, y];
            for (byte i = 0; i + 1 < biomRegions.Length; i++)
            {
                if (!(currentHeight >= biomRegions[i + 1].height))
                {
                    temperatureMap[x, y] = i;
                    break;
                }
            }
            return temperatureMap[x, y];
        }
        private float[,] noiseMap = new float[mapChunkSize, mapChunkSize];
        private float[,] noiseTemperatureMap = new float[mapChunkSize, mapChunkSize];
        public UnityAction<Vector2, byte, byte, Vector2> onSpawnPoint;
        private int[] countOfHeights = new int[9];
        private int count = 0, criticalHeight = 1;
        public ChunkData GenerateMapData(Vector2 centre)
        {
            FastRandom chunkRandom = new FastRandom((seed + (int)(centre.x + centre.y * centre.y)));
            SortedSet<Vector2> objectsToInst = new(new Vector2Comparer());
            byte[,] heightMap = new byte[mapChunkSize, mapChunkSize];
            byte[,] temperatureMap = new byte[mapChunkSize, mapChunkSize];
            if (Mathf.Abs(centre.x) > 10000 || Mathf.Abs(centre.y) > 10000)
                Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Local);
            else
                Noise.GenerateNoiseMap(ref noiseMap, centre * mapChunkSize, Noise.NormalizeMode.Global);

            Noise.GenerateTempNoiseMap(ref noiseTemperatureMap, centre * mapChunkSize);

            void SetSandPoint(int x, int y)
            {
                if (x >= 0 && x < mapChunkSize && y >= 0 && y < mapChunkSize && heightMap[x, y] > 1)
                    heightMap[x, y] = 2;
            }

            void SetNearWaterGrassPoint(int x, int y)
            {
                if (x >= 0 && x < mapChunkSize && y >= 0 && y < mapChunkSize && heightMap[x, y] > 2)
                    heightMap[x, y] = 3;
            }

            for (byte x = 0; x < mapChunkSize; x++)
            {
                for (byte y = 0; y < mapChunkSize; y++)
                {
                    float currentHeight = noiseMap[x, y];
                    for (byte i = 0; i < regions.Length; i++)
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
                    for (byte i = 0; i + 1 < biomRegions.Length; i++)
                    {
                        if (!(currentHeight >= biomRegions[i + 1].height))
                        {
                            temperatureMap[x, y] = i;
                            break;
                        }
                    }
                }
            }

            for (byte x = 1; x < mapChunkSize - 1; x++)
            {
                for (byte y = 1; y < mapChunkSize - 1; y++)
                {
                    if (temperatureMap[x, y] == criticalHeight && (
                    temperatureMap[x, y - 1] != temperatureMap[x, y] ||
                    temperatureMap[x - 1, y] != temperatureMap[x, y] ||
                    temperatureMap[x, y + 1] != temperatureMap[x, y] ||
                    temperatureMap[x + 1, y] != temperatureMap[x, y] ||
                    temperatureMap[x + 1, y + 1] != temperatureMap[x, y] ||
                    temperatureMap[x - 1, y + 1] != temperatureMap[x, y] ||
                    temperatureMap[x - 1, y - 1] != temperatureMap[x, y] ||
                    temperatureMap[x + 1, y - 1] != temperatureMap[x, y]))
                    {
                        heightMap[x, y] = 1;
                        heightMap[x - 1, y] = 1;
                        heightMap[x, y - 1] = 1;
                        heightMap[x, y + 1] = 1;
                        heightMap[x + 1, y] = 1;
                        SetSandPoint(x - 2, y);
                        SetSandPoint(x + 2, y);
                        SetSandPoint(x, y + 2);
                        SetSandPoint(x, y - 2);
                        SetSandPoint(x - 1, y - 1);
                        SetSandPoint(x - 1, y + 1);
                        SetSandPoint(x + 1, y - 1);
                        SetSandPoint(x + 1, y + 1);
                        SetNearWaterGrassPoint(x, y + 3);
                        SetNearWaterGrassPoint(x + 1, y + 2);
                        SetNearWaterGrassPoint(x + 2, y + 1);
                        SetNearWaterGrassPoint(x + 3, y);
                        SetNearWaterGrassPoint(x, y - 3);
                        SetNearWaterGrassPoint(x + 1, y - 2);
                        SetNearWaterGrassPoint(x + 2, y - 1);
                        SetNearWaterGrassPoint(x - 3, y);
                        SetNearWaterGrassPoint(x - 2, y + 1);
                        SetNearWaterGrassPoint(x - 1, y + 2);
                        SetNearWaterGrassPoint(x - 2, y - 1);
                        SetNearWaterGrassPoint(x - 1, y - 2);
                    }
                }
            }

            byte[,] tempTemperatureMap = GenerateTempMap(centre + new Vector2(0, 1));
            for (byte x = 1; x < mapChunkSize - 1; x++)
            {
                if (temperatureMap[x, mapChunkSize - 1] == criticalHeight && (
                    tempTemperatureMap[x, 0] != temperatureMap[x, mapChunkSize - 1] ||
                    tempTemperatureMap[x + 1, 0] != temperatureMap[x, mapChunkSize - 1] ||
                    tempTemperatureMap[x - 1, 0] != temperatureMap[x, mapChunkSize - 1] ||
                    temperatureMap[x, mapChunkSize - 2] != temperatureMap[x, mapChunkSize - 1] ||
                    temperatureMap[x - 1, mapChunkSize - 1] != temperatureMap[x, mapChunkSize - 1] ||
                    temperatureMap[x + 1, mapChunkSize - 1] != temperatureMap[x, mapChunkSize - 1] ||
                    temperatureMap[x - 1, mapChunkSize - 2] != temperatureMap[x, mapChunkSize - 1] ||
                    temperatureMap[x + 1, mapChunkSize - 2] != temperatureMap[x, mapChunkSize - 1]))
                {
                    heightMap[x, mapChunkSize - 1] = 1;
                    heightMap[x - 1, mapChunkSize - 1] = 1;
                    heightMap[x + 1, mapChunkSize - 1] = 1;

                    heightMap[x, mapChunkSize - 2] = 1;

                    SetSandPoint(x - 2, mapChunkSize - 1);
                    SetSandPoint(x + 2, mapChunkSize - 1);
                    SetSandPoint(x - 1, mapChunkSize - 2);
                    SetSandPoint(x + 1, mapChunkSize - 2);
                    SetSandPoint(x, mapChunkSize - 3);
                }
            }

            tempTemperatureMap = GenerateTempMap(centre - new Vector2(0, 1));
            for (byte x = 1; x < mapChunkSize - 1; x++)
            {
                if (temperatureMap[x, 0] == criticalHeight && (
                    tempTemperatureMap[x, mapChunkSize - 1] != temperatureMap[x, 0] ||
                    tempTemperatureMap[x - 1, mapChunkSize - 1] != temperatureMap[x, 0] ||
                    tempTemperatureMap[x + 1, mapChunkSize - 1] != temperatureMap[x, 0] ||
                    temperatureMap[x - 1, 0] != temperatureMap[x, 0] ||
                    temperatureMap[x, 1] != temperatureMap[x, 0] ||
                    temperatureMap[x + 1, 0] != temperatureMap[x, 0] ||
                    temperatureMap[x + 1, 1] != temperatureMap[x, 0] ||
                    temperatureMap[x - 1, 1] != temperatureMap[x, 0]))
                {
                    heightMap[x, 0] = 1;
                    heightMap[x - 1, 0] = 1;
                    heightMap[x + 1, 0] = 1;

                    heightMap[x, 1] = 1;

                    SetSandPoint(x + 2, 0);
                    SetSandPoint(x - 2, 0);
                    SetSandPoint(x + 1, 1);
                    SetSandPoint(x - 1, 1);
                    SetSandPoint(x, 2);
                }
            }

            tempTemperatureMap = GenerateTempMap(centre - new Vector2(1, 0));
            for (byte y = 1; y < mapChunkSize - 1; y++)
            {
                if (temperatureMap[0, y] == criticalHeight && (
                    tempTemperatureMap[mapChunkSize - 1, y] != temperatureMap[0, y] ||
                    tempTemperatureMap[mapChunkSize - 1, y - 1] != temperatureMap[0, y] ||
                    tempTemperatureMap[mapChunkSize - 1, y + 1] != temperatureMap[0, y] ||
                    temperatureMap[0, y - 1] != temperatureMap[0, y] ||
                    temperatureMap[0, y + 1] != temperatureMap[0, y] ||
                    temperatureMap[1, y] != temperatureMap[0, y] ||
                    temperatureMap[1, y + 1] != temperatureMap[0, y] ||
                    temperatureMap[1, y - 1] != temperatureMap[0, y]))
                {
                    heightMap[0, y] = 1;
                    heightMap[0, y - 1] = 1;
                    heightMap[0, y + 1] = 1;

                    heightMap[1, y] = 1;

                    SetSandPoint(0, y + 2);
                    SetSandPoint(0, y - 2);
                    SetSandPoint(1, y + 1);
                    SetSandPoint(1, y - 1);
                    SetSandPoint(2, y);
                }
            }

            tempTemperatureMap = GenerateTempMap(centre + new Vector2(1, 0));
            for (byte y = 1; y < mapChunkSize - 1; y++)
            {
                if (temperatureMap[mapChunkSize - 1, y] == criticalHeight && (
                    tempTemperatureMap[0, y] != temperatureMap[mapChunkSize - 1, y] ||
                    tempTemperatureMap[0, y - 1] != temperatureMap[mapChunkSize - 1, y] ||
                    tempTemperatureMap[0, y + 1] != temperatureMap[mapChunkSize - 1, y] ||
                    temperatureMap[mapChunkSize - 1, y - 1] != temperatureMap[mapChunkSize - 1, y] ||
                    temperatureMap[mapChunkSize - 2, y] != temperatureMap[mapChunkSize - 1, y] ||
                    temperatureMap[mapChunkSize - 1, y + 1] != temperatureMap[mapChunkSize - 1, y] ||
                    temperatureMap[mapChunkSize - 2, y + 1] != temperatureMap[mapChunkSize - 1, y] ||
                    temperatureMap[mapChunkSize - 2, y - 1] != temperatureMap[mapChunkSize - 1, y]))
                {
                    heightMap[mapChunkSize - 1, y] = 1;
                    heightMap[mapChunkSize - 1, y - 1] = 1;
                    heightMap[mapChunkSize - 1, y + 1] = 1;

                    heightMap[mapChunkSize - 2, y] = 1;

                    SetSandPoint(mapChunkSize - 1, y + 2);
                    SetSandPoint(mapChunkSize - 1, y - 2);
                    SetSandPoint(mapChunkSize - 2, y + 1);
                    SetSandPoint(mapChunkSize - 2, y - 1);
                    SetSandPoint(mapChunkSize - 3, y);
                }
            }

            if (temperatureMap[0, 0] == criticalHeight && (
                    GetTempMapPoint(centre - new Vector2(1, 1), mapChunkSize - 1, mapChunkSize - 1) != temperatureMap[0, 0] ||
                    GetTempMapPoint(centre - new Vector2(1, 0), mapChunkSize - 1, 0) != temperatureMap[0, 0] ||
                    GetTempMapPoint(centre - new Vector2(1, 0), mapChunkSize - 1, 1) != temperatureMap[0, 0] ||
                    GetTempMapPoint(centre - new Vector2(0, 1), 0, mapChunkSize - 1) != temperatureMap[0, 0] ||
                    GetTempMapPoint(centre - new Vector2(0, 1), 1, mapChunkSize - 1) != temperatureMap[0, 0] ||
                    temperatureMap[1, 1] != temperatureMap[0, 0] ||
                    temperatureMap[0, 1] != temperatureMap[0, 0] ||
                    temperatureMap[1, 0] != temperatureMap[0, 0]))
            {
                heightMap[0, 0] = 1;
                SetSandPoint(1, 0);
                SetSandPoint(0, 1);
                SetSandPoint(1, 1);
            }

            if (temperatureMap[mapChunkSize - 1, 0] == criticalHeight && (
                    GetTempMapPoint(centre + new Vector2(1, -1), 0, mapChunkSize - 1) != temperatureMap[mapChunkSize - 1, 0] ||
                    GetTempMapPoint(centre + new Vector2(1, 0), 0, 0) != temperatureMap[mapChunkSize - 1, 0] ||
                    GetTempMapPoint(centre + new Vector2(1, 0), 0, 1) != temperatureMap[mapChunkSize - 1, 0] ||
                    GetTempMapPoint(centre - new Vector2(0, 1), mapChunkSize - 1, mapChunkSize - 1) != temperatureMap[mapChunkSize - 1, 0] ||
                    GetTempMapPoint(centre - new Vector2(0, 1), mapChunkSize - 2, mapChunkSize - 1) != temperatureMap[mapChunkSize - 1, 0] ||
                    temperatureMap[mapChunkSize - 1, 1] != temperatureMap[mapChunkSize - 1, 0] ||
                    temperatureMap[mapChunkSize - 2, 1] != temperatureMap[mapChunkSize - 1, 0] ||
                    temperatureMap[mapChunkSize - 2, 0] != temperatureMap[mapChunkSize - 1, 0]))
            {
                heightMap[mapChunkSize - 1, 0] = 1;
                SetSandPoint(mapChunkSize - 1, 1);
                SetSandPoint(mapChunkSize - 2, 0);
                SetSandPoint(mapChunkSize - 2, 1);
            }

            if (temperatureMap[0, mapChunkSize - 1] == criticalHeight && (
                    GetTempMapPoint(centre + new Vector2(-1, 1), mapChunkSize - 1, 0) != temperatureMap[0, mapChunkSize - 1] ||
                    GetTempMapPoint(centre - new Vector2(1, 0), mapChunkSize - 1, mapChunkSize - 1) != temperatureMap[0, mapChunkSize - 1] ||
                    GetTempMapPoint(centre - new Vector2(1, 0), mapChunkSize - 1, mapChunkSize - 2) != temperatureMap[0, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(0, 1), 0, 0) != temperatureMap[0, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(0, 1), 1, 0) != temperatureMap[0, mapChunkSize - 1] ||
                    temperatureMap[0, mapChunkSize - 2] != temperatureMap[0, mapChunkSize - 1] ||
                    temperatureMap[1, mapChunkSize - 1] != temperatureMap[0, mapChunkSize - 1] ||
                    temperatureMap[1, mapChunkSize - 2] != temperatureMap[0, mapChunkSize - 1]))
            {
                heightMap[0, mapChunkSize - 1] = 1;
                SetSandPoint(1, mapChunkSize - 1);
                SetSandPoint(0, mapChunkSize - 2);
                SetSandPoint(1, mapChunkSize - 2);
            }


            if (temperatureMap[mapChunkSize - 1, mapChunkSize - 1] == criticalHeight && (
                    GetTempMapPoint(centre + new Vector2(1, 1), 0, 0) != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(1, 0), 0, mapChunkSize - 1) != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(1, 0), 0, mapChunkSize - 2) != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(0, 1), mapChunkSize - 1, 0) != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    GetTempMapPoint(centre + new Vector2(0, 1), mapChunkSize - 2, 0) != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    temperatureMap[mapChunkSize - 1, mapChunkSize - 2] != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    temperatureMap[mapChunkSize - 2, mapChunkSize - 1] != temperatureMap[mapChunkSize - 1, mapChunkSize - 1] ||
                    temperatureMap[mapChunkSize - 2, mapChunkSize - 2] != temperatureMap[mapChunkSize - 1, mapChunkSize - 1]))
            {
                heightMap[mapChunkSize - 1, mapChunkSize - 1] = 1;
                SetSandPoint(mapChunkSize - 2, mapChunkSize - 1);
                SetSandPoint(mapChunkSize - 1, mapChunkSize - 2);
                SetSandPoint(mapChunkSize - 2, mapChunkSize - 2);
            }

            bool isEqual = true;
            for (byte x = 0; x < mapChunkSize; x++)
            {
                for (byte y = 0; y < mapChunkSize; y++)
                {
                    if (heightMap[x, y] != heightMap[0, 0])
                        isEqual = false;
                    if (!endlessFlag[2])
                        continue;
                    countOfHeights[heightMap[x, y]]++;
                    bool structHere = false;
                    TemperatureLevel level = regions[heightMap[x, y]].level[temperatureMap[x, y]];
                    for (byte i = 0; i < level.structs.Length; i++)
                    {
                        // StructInfoGeneration sinfo = level.structs[i];
                        // if(sinfo.Chance == 0)
                        //     continue;
                        // if ((x * y + centre.x * centre.y + Seed + i * countOfHeights[heightMap[x, y]] + count) % sinfo.Chance == 0)
                        // {
                        //     Vector2 posstruct = new(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale);
                        //     var WFCAobjects = WFCA(posstruct, (byte)((Seed + (int)x + (int)y) % sinfo.info.tileInfo.Length), sinfo.info);
                        //     foreach (var item in WFCAobjects)
                        //     {
                        //         if (objectSystem.TryAddToGlobal(item.Key, item.Value.prefab.GetInstanceID(), item.Value.amount, item.Value.iType, (x + y) % 2 == 0))
                        //             {
                        //                 var data = GetMapDataByObjectPosition(item.Key);
                        //                 // objectsToInst.Add(item.Key);
                        //             }
                        //     }
                        //     structHere = true;
                        //     if (sinfo.isSpawnPoint)
                        //         onSpawnPoint?.Invoke(posstruct, heightMap[x, y], temperatureMap[x, y], centre);
                        //     break;
                        // }
                    }
                    if (structHere) continue;
                    for (byte i = 0; i < level.objects.Length; i++)
                    {
                        ObjectInfoGeneration ginfo = level.objects[i];
                        if(ginfo.Chance == 0 || ginfo.info == null) continue;
                        if (chunkRandom.Range(0, 1000) < ginfo.Chance)
                        {
                            Vector2 posobj = new(centre.x * generationSize + x * scale, centre.y * generationSize + y * scale);
                           
                                if (objectSystem.TryAddToGlobal(posobj, ginfo.info.prefab.GetInstanceID(), ginfo.info.amount, ginfo.info.iType, (x + y) % 2 == 0))
                                {
                                    objectsToInst.Add(posobj);
                                    break;
                                }
                        }
                    }
                    count += x + y;
                }
            }
            return new ChunkData(heightMap, temperatureMap, isEqual, objectsToInst);
        }
        private Queue<Pair<Vector2, byte>> WFCAqueue = new(16);
        // private Dictionary<Vector2, ObjectInfo> WFCA(Vector2 curPos, byte type, StructInfo structInfo)
        // {
        //     Dictionary<Vector2, ObjectInfo> WFCAobjects = new(8);

        //     Debug.Log("wfca");
        //     WFCAqueue.Clear();
        //     byte maxIteration = 0, count = 0;
        //     for (byte i = 0; i < structInfo.tileInfo.Length; i++)
        //         maxIteration += structInfo.tileInfo[i].MCount;
        //     byte[] Count = new byte[9];
        //     count++;
        //     WFCAqueue.Enqueue(new Pair<Vector2, byte>(curPos, type));
        //     while (WFCAqueue.Count != 0)
        //     {
        //         Pair<Vector2, byte> current = WFCAqueue.Dequeue();
        //         if (count > maxIteration) break;
        //         if (structInfo.tileInfo[current.Second].MCount > Count[current.Second] && !WFCAobjects.ContainsKey(current.First))
        //         {
        //             WFCAobjects[current.First] = structInfo.tileInfo[current.Second].objectInfo;
        //             Count[current.Second]++;
        //             count++;
        //         }
        //         byte c = 0;
        //         for (sbyte x = -1; x <= 1; x++)
        //         {
        //             for (sbyte y = -1; y <= 1; y++)
        //             {
        //                 if (x == 0 && y == 0) continue;
        //                 Vector2 newPos = current.First + new Vector2(x, y) * structInfo.distortion;
        //                 byte field = structInfo.tileInfo[current.Second].height[c++];
        //                 if (field == 0) continue;
        //                 if (WFCAobjects.ContainsKey(newPos)) continue;
        //                 WFCAqueue.Enqueue(new Pair<Vector2, byte>(newPos, --field));
        //             }
        //         }
        //     }
        //     return WFCAobjects;
        // }
        private Vector2 OldVposition, position;
        public UnityAction<Vector2> onUpdate;
        private async UniTaskVoid GenerationUpdate()
        {
            position = GetPlayerPosition();
            for (byte i = 0; i < 3; i++)
            {
                if(!endlessFlag[i]) continue;
                endless[i].UpdateChunk(position);
                await UniTask.WaitForFixedUpdate();
            }
            while (!DataStorage.sceneClose)
            {
                position = GetPlayerPosition();
                if (position != OldVposition && rotateTarget == 0f)
                {
                    OldVposition = position;
                    byte extendedChunk = chunkScale + 1;
                    for (int yOffset = -extendedChunk; yOffset <= extendedChunk; yOffset++)
                    {
                        for (int xOffset = -extendedChunk; xOffset <= extendedChunk; xOffset++)
                        {
                            Vector2 chunkPosition = new(position.x + xOffset, position.y + yOffset);
                            if(!mapData.ContainsKey(chunkPosition)) mapData[chunkPosition] = GenerateMapData(chunkPosition);
                        }
                    }
                    for (byte i = 0; i < 3; i++)
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

        private Vector2 GetPlayerPosition() => Extension.RoundVector2D((viewer.position - new Vector3(generationSize, generationSize) / 2) / (generationSize));
        public void RotateBasis(sbyte angle)
        {
            // if (rotateValue != 0f)
            //     return;
            // if (angle == 90)
            // {
            //     if (rotateTarget != 0f)
            //         return;
            //     RotationCome().Forget();
            // }
            // else if (angle == -90)
            // {
            //     if (rotateTarget != 270f)
            //         return;
            //     RotationBack().Forget();
            // }
        }
        public float rotateValue = 0f, rotateTarget = 0f;
        // private static EnumerableSnapshot<int> objectsSnapshot;
        // private Dictionary<int, ushort> objectUpdate = new Dictionary<int, ushort>(16);
        // List<Vector2> chunkCoord = new List<Vector2>();
        // private async UniTaskVoid RotationCome()
        // {
        //     rotateValue = 0.1f;
        //     rotateTarget = 270f;
        //     Vector2 Vposition = GetPlayerPosition();
        //     ObjectInfo[] prefabInfo = objectSystem._info;
        //     for (int i = 0; i < prefabInfo.Length; i++)
        //         objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
        //     for (byte yOffset = 0; yOffset < chunkCount; yOffset++)
        //         for (byte xOffset = 0; xOffset < chunkCount; xOffset++)
        //             chunkCoord.Add(new Vector2(Vposition.x + xOffset, Vposition.y + yOffset));
        //     do
        //     {
        //         foreach (var vector in chunkCoord)
        //         {
        //             foreach (var item in GetMapData(vector).objectsToInst)
        //             {
        //                 ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
        //                 if (info.prefabID == 0)
        //                     continue;
        //                 objectUpdate[info.prefabID]++;
        //                 objectSystem.Reuse(info.prefabID, item, info.flip, rotateValue);
        //             }
        //         }
        //         objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        //         foreach (var ID in objectsSnapshot)
        //         {
        //             for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
        //                 objectSystem.Deactivate(ID);
        //             objectUpdate[ID] = 0;
        //         }
        //         objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
        //         viewer.Rotate(0, 0, rotateValue, Space.Self);
        //         await UniTask.WaitForFixedUpdate();
        //     } while (objectSystem.transform.eulerAngles.z > rotateTarget);
        //     rotateValue = 0f;
        //     await UniTask.WaitForFixedUpdate();
        // }

        // private async UniTaskVoid RotationBack()
        // {
        //     rotateValue = -0.1f;
        //     rotateTarget = 360f;
        //     Vector2 Vposition = GetPlayerPosition();
        //     ObjectInfo[] prefabInfo = objectSystem._info;
        //     for (int i = 0; i < prefabInfo.Length; i++)
        //         objectUpdate[prefabInfo[i].prefab.GetInstanceID()] = 0;
        //     do
        //     {
        //         foreach (var vector in chunkCoord)
        //         {
        //             foreach (var item in GetMapData(vector).objectsToInst)
        //             {
        //                 ObjectInstInfo info = objectSystem.GetGlobalObjectInfo(item);
        //                 if (info.prefabID == 0)
        //                     continue;
        //                 objectUpdate[info.prefabID]++;
        //                 objectSystem.Reuse(info.prefabID, item, info.flip, rotateValue);
        //             }
        //         }
        //         objectsSnapshot = objectUpdate.Keys.ToEnumerableSnapshot();
        //         foreach (var ID in objectsSnapshot)
        //         {
        //             for (byte j = 0; j < objectSystem.GetPrefabInfo(ID).poolSize - objectUpdate[ID]; j++)
        //                 objectSystem.Deactivate(ID);
        //             objectUpdate[ID] = 0;
        //         }
        //         objectSystem.transform.Rotate(0, 0, -rotateValue, Space.World);
        //         viewer.Rotate(0, 0, rotateValue, Space.Self);
        //         await UniTask.WaitForFixedUpdate();
        //     } while ((int)objectSystem.transform.eulerAngles.z != 0);
        //     rotateValue = 0f;
        //     rotateTarget = 0f;
        //     await UniTask.WaitForFixedUpdate();
        // }

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
            WFCAqueue.Clear();
        }

        public Transform viewerTest;

        // public void TestGeneration()
        // {
        //     if (endlessFlag[0])
        //         endless[0] = new EndlessTerrain(this);
        //     if (endlessFlag[1])
        //         endless[1] = new EndlessLiquids(this);
        //     endlessFlag[2] = false;
        //     for (sbyte i = -scale; i < scale; i++)
        //     {
        //         for (sbyte j = -scale; j < scale; j++)
        //         {
        //             Vector2 centre = new(i, j);
        //             mapData[centre] = GenerateMapData(centre);
        //         }
        //     }
        //     for (byte i = 0; i < 2; i++)
        //         endless[i].UpdateChunk(Extension.RoundVector2D(viewerTest.position / (scale * mapChunkSize)));
        // }
    }

    [System.Serializable]
    public struct TerrainType
    {
        public float height;
        public bool liveAble;
        public TemperatureLevel[] level;
    }

    [System.Serializable]
    public struct ObjectInfoGeneration
    {
        public byte Chance;
        public ObjectInfo info;
    }

    [System.Serializable]
    public struct StructInfoGeneration
    {
        public bool isSpawnPoint;
        public ushort Chance;
        public StructInfo info;
    }

    [System.Serializable]
    public struct TemperatureType
    {
        public float height;
    }

    [System.Serializable]
    public struct TemperatureLevel
    {
        public ObjectInfoGeneration[] objects;
        public StructInfoGeneration[] structs;
    }
    public class ChunkData
    {
        public readonly byte[,] heightMap, temperatureMap;
        public readonly bool isEqual;
        public SortedSet<Vector2> objectsToInst;
        public ChunkData(byte[,] heightMap, byte[,] temperatureMap, bool isEqual, SortedSet<Vector2> objectsToInst)
        {
            this.heightMap = heightMap;
            this.temperatureMap = temperatureMap;
            this.isEqual = isEqual;
            this.objectsToInst = objectsToInst;
        }
    }

}