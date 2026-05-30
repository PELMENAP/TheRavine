using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using System.Threading;

using TheRavine.Extensions;
using TheRavine.ObjectControl;
using R3;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    using TheRavine.Base;

    public class MapGenerator : MonoBehaviour, ISetAble
    {
        [SerializeField] private ChunkGenerationSettings chunkGenerationSettings;
        private readonly CancellationTokenSource _cts = new();

        public const int mapChunkSize = 40, chunkScale = 1, scale = 2;
        public const int generationSize = scale * mapChunkSize;
        public const float maxTerrainHeight = 50f;

        private Dictionary<Vector2Int, ChunkData> mapData;

        public ChunkData GetMapData(Vector2Int position)
        {
            if (mapData.TryGetValue(position, out ChunkData data))
                return data;

            data = chunkGenerator.GenerateMapData(position);
            mapData[position] = data;
            return data;
        }
        public bool IsHeightIsLiveAble(int height) =>
            chunkGenerationSettings.regions[height].liveAble;

        public bool IsWaterHeight(Vector2Int position) =>
            GetMapHeight(position) < 1;

        public int GetMapHeight(Vector2 pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);
            Vector2Int local = GetLocalPosition(pos);
            return GetMapData(chunk).HeightMap[Idx(local.x, local.y)];
        }

        public Vector3 GetRealPosition(Vector2Int pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);
            Vector2Int local = GetLocalPosition(pos);
            float height = GetMapData(chunk).HeightRaw[Idx(local.x, local.y)];
            return new Vector3(pos.x, height * maxTerrainHeight, pos.y);
        }
        public Vector2Int GetChunkPosition(Vector2 pos) =>
            new(Mathf.FloorToInt(pos.x / generationSize),
                Mathf.FloorToInt(pos.y / generationSize));

        public Vector2Int GetLocalPosition(Vector2 pos)
        {
            Vector2Int chunk = GetChunkPosition(pos);
            float lx = pos.x - chunk.x * generationSize;
            float ly = pos.y - chunk.y * generationSize;
            return new Vector2Int(
                Mathf.FloorToInt(lx / scale),
                Mathf.FloorToInt(ly / scale));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(int x, int y) => y * mapChunkSize + x;

        public bool TryAddObject(
            Vector2Int worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            InstanceType instanceType,
            Vector2Int[] additionalWorldCells = null)
        {
            Vector2Int chunk = GetChunkPosition(worldPos);
            Vector2Int local = GetLocalPosition(worldPos);
            ChunkData cd = GetMapData(chunk);

            int primaryIdx = Idx(local.x, local.y);

            // Для ячеек, занятых многоклеточным объектом
            int[] additionalLocalIdxs = null;
            if (additionalWorldCells != null && additionalWorldCells.Length > 0)
            {
                additionalLocalIdxs = new int[additionalWorldCells.Length];
                for (int i = 0; i < additionalWorldCells.Length; i++)
                {
                    Vector2Int aw = additionalWorldCells[i];
                    // Дополнительные ячейки могут попасть в другой чанк —
                    // в текущей реализации ограничиваемся одним чанком.
                    Vector2Int ac = GetChunkPosition(aw);
                    if (ac != chunk)
                    {
                        Debug.LogWarning(
                            $"[MapGenerator] AdditionalCell {aw} falls outside " +
                            $"primary chunk {chunk}. Cross-chunk multi-cell objects " +
                            "are not supported yet.");
                        return false;
                    }
                    Vector2Int al = GetLocalPosition(aw);
                    additionalLocalIdxs[i] = Idx(al.x, al.y);
                }
            }

            var info = new ObjectInstInfo(realPos, prefabID, amount, instanceType);
            return cd.TryAddObject(primaryIdx, in info, additionalLocalIdxs);
        }
        public bool TryGetObject(Vector2Int worldPos, out ObjectInstInfo info)
        {
            Vector2Int chunk = GetChunkPosition(worldPos);
            Vector2Int local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
            {
                info = default;
                return false;
            }

            return cd.TryGetObject(Idx(local.x, local.y), out info);
        }
        public bool RemoveObject(Vector2Int worldPos)
        {
            Vector2Int chunk = GetChunkPosition(worldPos);
            Vector2Int local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            return cd.RemoveObject(Idx(local.x, local.y));
        }
        public bool ContainsObject(Vector2Int worldPos)
        {
            Vector2Int chunk = GetChunkPosition(worldPos);
            Vector2Int local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            return cd.Occupancy[Idx(local.x, local.y)] != 0;
        }

        /// <summary>
        /// Добавить позицию в список объектов чанка вручную (напр. после постройки игроком).
        /// </summary>
        public bool TryToAddPositionToChunk(Vector2Int worldPos)
        {
            if (ContainsObject(worldPos))
                return false;

            // Создаём пустую запись-маркер, реальная ObjectInstInfo добавляется через TryAddObject.
            // Этот метод оставлен для совместимости; предпочтительнее использовать TryAddObject.
            Vector2Int chunk = GetChunkPosition(worldPos);
            Vector2Int local = GetLocalPosition(worldPos);
            ChunkData cd = GetMapData(chunk);

            var placeholder = new ObjectInstInfo(
                GetRealPosition(worldPos), -1, 0, InstanceType.Static);

            return cd.TryAddObject(Idx(local.x, local.y), in placeholder);
        }
        public void UnloadChunk(Vector2Int position)
        {
            if (mapData.TryGetValue(position, out ChunkData cd))
            {
                cd.Dispose();
                mapData.Remove(position);
            }
        }
        public Transform terrainTransform, waterTransform;
        public MeshFilter terrainFilter;
        public MeshCollider terrainCollider;
        private int seed;
        public int Seed { get => seed; private set => seed = value; }

        private ObjectSystem objectSystem;
        private NAL_PC nal;

        [SerializeField] private Transform viewer;
        [SerializeField] private Vector3 viewerOffset;

        public ChunkGenerator chunkGenerator;
        public Vector3 waterOffset;

        private IEndless[] endless;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);

            seed = 16;
            mapData = new Dictionary<Vector2Int, ChunkData>(64);

            Noise.SetInit(
                chunkGenerationSettings.heightNoiseSettings,
                chunkGenerationSettings.riverNoiseSettings,
                chunkGenerationSettings.temperatureSettings,
                chunkGenerationSettings.moistureSettings,
                Seed,
                mapChunkSize);

            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            chunkGenerator = new ChunkGenerator(objectSystem, chunkGenerationSettings, this);

            ServiceLocator.WhenPlayersNonEmpty()
                .Subscribe(_ =>
                    GetViewers(ServiceLocator.Players.GetAllPlayersTransform()));

            if (ServiceLocator.Services.TryGet(out WorldRegistry worldRegistry))
            {
                var config = worldRegistry.GetCurrentConfig();
                chunkGenerationSettings.isRiver = config.generateRivers;
            }

            if (chunkGenerationSettings.endlessFlag[3])
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
            if (chunkGenerationSettings.endlessFlag[0])
                endless[0] = new EndlessTerrain(this, chunkGenerationSettings);
            if (chunkGenerationSettings.endlessFlag[1])
                endless[1] = new EndlessLiquids(this);
            if (chunkGenerationSettings.endlessFlag[2])
                endless[2] = new EndlessObjects(this, objectSystem);
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

        private void GetViewers(IReadOnlyList<Transform> players) =>
            viewer = players[0];

        public void ClearNALQueue() => nal?.Clear();
        public void AddNALObject(Vector2Int pos) => nal?.Enqueue(pos);

        private Vector2Int _oldVposition, _position;
        public UnityAction<Vector2Int> onUpdate;

        private async UniTaskVoid GenerationUpdate()
        {
            _position = GetPlayerPosition();

            for (int i = 0; i < 3; i++)
            {
                if (!chunkGenerationSettings.endlessFlag[i]) continue;
                endless[i].UpdateChunk(_position);
                await UniTask.WaitForFixedUpdate();
            }

            while (!_cts.Token.IsCancellationRequested)
            {
                _position = GetPlayerPosition();

                if (_position != _oldVposition)
                {
                    _oldVposition = _position;

                    int extendedChunk = chunkScale + 1;
                    for (int yOff = -extendedChunk; yOff <= extendedChunk; yOff++)
                    for (int xOff = -extendedChunk; xOff <= extendedChunk; xOff++)
                    {
                        Vector2Int cp = new(_position.x + xOff, _position.y + yOff);
                        if (!mapData.ContainsKey(cp))
                            mapData[cp] = chunkGenerator.GenerateMapData(cp);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        if (!chunkGenerationSettings.endlessFlag[i]) continue;
                        endless[i].UpdateChunk(_position);
                        await UniTask.WaitForFixedUpdate();
                    }

                    onUpdate?.Invoke(_position);
                }

                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }

        public void ExtraUpdate()
        {
            _position = GetPlayerPosition();
            for (int i = 0; i < 3; i++)
            {
                if (!chunkGenerationSettings.endlessFlag[i]) continue;
                endless[i].UpdateChunk(_position);
            }
        }

        private Vector2Int GetPlayerPosition()
        {
            if (viewer == null) return Vector2Int.zero;
            Vector3 p = new(
                viewer.position.x - generationSize / 2f + viewerOffset.x,
                viewer.position.z + generationSize / 2f + viewerOffset.z,
                0);
            return Extension.RoundVector2D(p / generationSize);
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            foreach (var cd in mapData.Values) cd.Dispose();
            mapData.Clear();
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            _cts.Cancel();
            chunkGenerator.Dispose();
        }
    }

    public sealed class ChunkData : IDisposable
    {
        private const int Size = MapGenerator.mapChunkSize;
        public const int TotalCells = Size * Size;
        public NativeArray<float> HeightRaw;
        public readonly NativeArray<int> HeightMap;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetHeight(int x, int y) => HeightRaw[y * MapGenerator.mapChunkSize + x];


        public readonly NativeArray<int> BiomeMap;

        /// <summary>
        /// Занятость ячейки объектами:
        ///   0         — пусто
        ///   positive N → Objects[N-1] (первичная ячейка объекта)
        ///   negative N → Objects[(-N)-1] (вторичная ячейка многоклеточного объекта)
        /// </summary>
        public NativeArray<int> Occupancy;
        public NativeList<ObjectInstInfo> Objects;

        public List<StructureSpawnPoint> StructureSpawnPoints;

        public bool IsDirty { get; private set; }

        public ChunkData()
        {
            HeightRaw  = new NativeArray<float>(TotalCells, Allocator.Persistent);
            HeightMap  = new NativeArray<int>(TotalCells,   Allocator.Persistent);
            BiomeMap   = new NativeArray<int>(TotalCells,   Allocator.Persistent);
            Occupancy  = new NativeArray<int>(TotalCells,   Allocator.Persistent);
            Objects    = new NativeList<ObjectInstInfo>(16, Allocator.Persistent);
        }

        public bool TryAddObject(
            int primaryIdx,
            in ObjectInstInfo info,
            int[] additionalIdxs = null)
        {
            if ((uint)primaryIdx >= (uint)TotalCells) return false;
            if (Occupancy[primaryIdx] != 0)           return false;

            if (additionalIdxs != null)
            {
                foreach (int ac in additionalIdxs)
                {
                    if ((uint)ac >= (uint)TotalCells) return false;
                    if (Occupancy[ac] != 0)           return false;
                }
            }

            int handle = Objects.Length + 1; // 1-based, 0 = empty
            Objects.Add(info);
            Occupancy[primaryIdx] = handle;

            if (additionalIdxs != null)
                foreach (int ac in additionalIdxs)
                    Occupancy[ac] = -handle; // вторичная ячейка

            IsDirty = true;
            return true;
        }

        public bool TryGetObject(int cellIdx, out ObjectInstInfo info)
        {
            info = default;
            if ((uint)cellIdx >= (uint)TotalCells) return false;

            int h = Occupancy[cellIdx];
            if (h == 0) return false;

            int objIdx = (h > 0 ? h : -h) - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            info = Objects[objIdx];
            return true;
        }
        public bool RemoveObject(int cellIdx)
        {
            if ((uint)cellIdx >= (uint)TotalCells) return false;

            int h = Occupancy[cellIdx];
            if (h == 0) return false;

            int primaryHandle = h > 0 ? h : -h;

            for (int i = 0; i < TotalCells; i++)
            {
                int oh = Occupancy[i];
                if (oh == primaryHandle || oh == -primaryHandle)
                    Occupancy[i] = 0;
            }

            int objIdx = primaryHandle - 1;
            int last   = Objects.Length - 1;

            if (objIdx != last)
            {
                Objects[objIdx] = Objects[last];

                int oldH = last + 1;
                int newH = objIdx + 1;
                for (int i = 0; i < TotalCells; i++)
                {
                    int oh = Occupancy[i];
                    if      (oh ==  oldH) Occupancy[i] =  newH;
                    else if (oh == -oldH) Occupancy[i] = -newH;
                }
            }

            Objects.RemoveAt(last);
            IsDirty = true;
            return true;
        }

        public void ClearDirty() => IsDirty = false;
        public void Dispose()
        {
            if (HeightRaw.IsCreated) HeightRaw.Dispose();
            if (HeightMap.IsCreated) HeightMap.Dispose();
            if (BiomeMap.IsCreated)  BiomeMap.Dispose();
            if (Occupancy.IsCreated) Occupancy.Dispose();
            if (Objects.IsCreated)   Objects.Dispose();
        }
    }

    public readonly struct StructureSpawnPoint
    {
        public readonly Vector2Int WorldPosition;
        public readonly GenerationSettingsSO WfcSettings;
        public readonly TilePatternSO InitialPattern;

        public StructureSpawnPoint(
            Vector2Int pos,
            GenerationSettingsSO settings,
            TilePatternSO pattern)
        {
            WorldPosition  = pos;
            WfcSettings    = settings;
            InitialPattern = pattern;
        }
    }
}