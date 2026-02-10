using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

using TheRavine.Extensions;
using TheRavine.ObjectControl;
using R3;

namespace TheRavine.Generator
{
    using System;
    using EndlessGenerators;
    public class MapGenerator : MonoBehaviour, ISetAble
    {
        [SerializeField] private ChunkGenerationSettings chunkGenerationSettings;
        private readonly CancellationTokenSource _cts = new();
        public const int mapChunkSize = 40, chunkScale = 1, scale = 2, generationSize = scale * mapChunkSize;
        private Dictionary<Vector2Int, ChunkData> mapData;
        public ChunkData GetMapData(Vector2Int position)
        {
            if (mapData.TryGetValue(position, out ChunkData data))
                return data;
            mapData[position] = chunkGenerator.GenerateMapData(position);
            return mapData[position];
        }

        public bool IsHeightIsLiveAble(int height) => regions[height].liveAble;
        public bool IsWaterHeight(Vector2Int position) => GetMapHeight(position) < 1;

        public int GetMapHeight(Vector2 pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);
            Vector2Int local = GetLocalPosition(pos);

            return GetMapData(chunk).heightMap[local.x, local.y];
        }

        public Vector3 GetRealPosition(Vector2Int pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);
            Vector2Int local = GetLocalPosition(pos);
            float height = GetMapData(chunk).heightMap[local.x, local.y];
            return new Vector3(pos.x, height, pos.y);
        }

        public Vector2Int GetChunkPosition(Vector2 pos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(pos.x / generationSize),
                Mathf.FloorToInt(pos.y / generationSize)
            );
        }

        public Vector2Int GetLocalPosition(Vector2 pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);

            float localX = pos.x - chunk.x * generationSize;
            float localY = pos.y - chunk.y * generationSize;

            return new Vector2Int(
                Mathf.FloorToInt(localX / scale),
                Mathf.FloorToInt(localY / scale)
            );
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
        private NAL_PC nal;
        [SerializeField] private float noiseScale;
        [SerializeField] private int octaves;
        [SerializeField, Range(0, 1)] private float persistence;
        [SerializeField] private float lacunarity;
        [SerializeField] private TerrainType[] regions;
        [SerializeField] private TemperatureType[] biomRegions;
        [SerializeField] private Transform viewer;
        [SerializeField] private Vector3 viewerOffset;
        [SerializeField] private bool[] endlessFlag;
        [SerializeField] private bool isRiver;
        public ChunkGenerator chunkGenerator;
        public Vector3 waterOffset;
        private IEndless[] endless;
        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);
            
            // seed = RavineRandom.RangeInt(0, 1000);
            seed = 16;
            mapData = new Dictionary<Vector2Int, ChunkData>(64);
            Noise.SetInit(noiseScale, octaves, persistence, lacunarity, Seed);
            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            chunkGenerator = new ChunkGenerator(objectSystem, chunkGenerationSettings);

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                {
                    GetViewers(ServiceLocator.Players.GetAllPlayersTransform());
                });


            if (endlessFlag[3])
            {
                nal = new NAL_PC(this, objectSystem, _cts.Token);
                nal.RunNAL().Forget();
                nal.RunUpdate().Forget();
            }

            SetupEndless();
            FirstInstance().Forget();
            callback?.Invoke();
        }
        
        private void SetupEndless()
        {
            endless = new IEndless[3];
            if (endlessFlag[0]) endless[0] = new EndlessTerrain(this);
            if (endlessFlag[1]) endless[1] = new EndlessLiquids(this);
            if (endlessFlag[2]) endless[2] = new EndlessObjects(this, objectSystem);
        }

        private async UniTaskVoid FirstInstance()
        {
            for (int i = -scale; i < scale; i++)
            {
                for (int j = -scale; j < scale; j++)
                {
                    Vector2Int centre = new(i, j);
                    mapData[centre] = chunkGenerator.GenerateMapData(centre);
                    await UniTask.Delay(50);
                }
            }
            GenerationUpdate().Forget();
        }

        private void GetViewers(IReadOnlyList<Transform> players)
        {
            viewer = players[0];
        }

        public void ClearNALQueue() => nal?.Clear();

        public void AddNALObject(Vector2Int pos) => nal?.Enqueue(pos);

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

                if (position != OldVposition)
                {
                    OldVposition = position;
                    int extendedChunk = chunkScale + 1;
                    for (int yOffset = -extendedChunk; yOffset <= extendedChunk; yOffset++)
                    {
                        for (int xOffset = -extendedChunk; xOffset <= extendedChunk; xOffset++)
                        {
                            Vector2Int chunkPosition = new(position.x + xOffset, position.y + yOffset);
                            if(!mapData.ContainsKey(chunkPosition)) mapData[chunkPosition] = chunkGenerator.GenerateMapData(chunkPosition);
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
            position = GetPlayerPosition();
            // if(!endlessFlag[2]) return;
            // endless[2].UpdateChunk(position);

            for (int i = 0; i < 3; i++)
            {
                if(!endlessFlag[i]) continue;
                endless[i].UpdateChunk(position);
            }
        }

        private Vector2Int GetPlayerPosition()
        {
            if (viewer == null) return Vector2Int.zero;
            Vector3 playerPosition = new(viewer.position.x - generationSize / 2 + viewerOffset.x, viewer.position.z + generationSize / 2 + viewerOffset.z, 0);
            return Extension.RoundVector2D(playerPosition / generationSize);
        }

        public void BreakUp(ISetAble.Callback callback)
        {
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

    [Serializable]
    public class ChunkGenerationSettings
    {
        public int rareness, seed, farlands;
        public bool isRiver;
        public bool[] endlessFlag;
        public float riverMin = 0.45f,  riverMax = 0.6f, riverInfluence = 0.05f, maxRiverDepth = 0.3f;

        public TerrainType[] regions;
        public TemperatureType[] biomRegions;
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