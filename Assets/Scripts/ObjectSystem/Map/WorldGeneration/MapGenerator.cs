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
    using Unity.Mathematics;

    public class MapGenerator : MonoBehaviour, ISetAble
    {
        [SerializeField] private ChunkGenerationSettings chunkGenerationSettings;
        private readonly CancellationTokenSource _cts = new();

        public const int mapChunkSize = 64, chunkScale = 1, scale = 2;
        public const int generationSize = scale * mapChunkSize;
        public const float maxTerrainHeight = 50f;

        private Dictionary<long, ChunkData> mapData = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(long chunkKey)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData data))
                return data;
            data = chunkGenerator.GenerateMapData(chunkKey);
            
            mapData[chunkKey] = data;
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(int x, int y) => 
            GetMapData(Position2Int.Pack(x, y));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasChunk(long chunkKey) => mapData.ContainsKey(chunkKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetChunk(long chunkKey, out ChunkData data) => 
            mapData.TryGetValue(chunkKey, out data);

        public void RemoveChunk(long chunkKey) => mapData.Remove(chunkKey);
        public bool IsHeightIsLiveAble(long position) =>
            GetRealPosition(position).y > 5;

        public bool IsWaterHeight(long position) =>
            GetRealPosition(position).y < 5;

        public Vector3 GetRealPosition(long pos)
        {
            long chunk = GetPosition2Int(pos);
            long local = GetLocalPosition(pos);
            
            float height = GetMapData(chunk).HeightRaw[Idx(Position2Int.GetX(local), Position2Int.GetY(local))];
            return new Vector3(Position2Int.GetX(pos), height, Position2Int.GetY(pos));
        }
        public long GetPosition2Int(long pos)
        {
            int x = Mathf.FloorToInt(Position2Int.GetX(pos) / generationSize);
            int y = Mathf.FloorToInt(Position2Int.GetY(pos) / generationSize);
            return Position2Int.Pack(x, y);
        }

        public long GetLocalPosition(long pos)
        {
            long chunk = GetPosition2Int(pos);
            float lx = Position2Int.GetX(pos) - Position2Int.GetX(chunk) * generationSize;
            float ly = Position2Int.GetY(pos) - Position2Int.GetY(chunk) * generationSize;

            return Position2Int.Pack(Mathf.FloorToInt(lx / scale), Mathf.FloorToInt(ly / scale));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetChunkAndIndex(
            float wx,
            float wz,
            out ChunkData chunk,
            out int index)
        {
            float gx = wx / scale;
            float gz = wz / scale;

            int chunkX = Mathf.FloorToInt(gx / mapChunkSize);
            int chunkZ = Mathf.FloorToInt(gz / mapChunkSize);

            int localX =
                Mathf.FloorToInt(
                    gx - chunkX * mapChunkSize);

            int localZ =
                Mathf.FloorToInt(
                    gz - chunkZ * mapChunkSize);

            chunk = GetMapData(chunkX, chunkZ);

            index = localZ * mapChunkSize + localX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSpeedModifier(
            float x,
            float z,
            float2 moveDirection)
        {
            float3 normal =
                SampleNormal(
                    x,
                    z);

            float slopeAngle =
                math.degrees(
                    math.acos(
                        math.clamp(
                            normal.y,
                            -1f,
                            1f)));

            float2 uphill =
                math.normalizesafe(
                    new float2(
                        -normal.x,
                        -normal.z));

            float uphillDot =
                math.dot(
                    math.normalize(moveDirection),
                    uphill);

            if (uphillDot > 0f &&
                slopeAngle > 70f)
            {
                return 0f;
            }

            float modifier =
                1f -
                slopeAngle / 70f;

            if (uphillDot < 0f)
            {
                modifier =
                    math.lerp(
                        modifier,
                        1f,
                        -uphillDot * 0.25f);
            }

            return math.saturate(modifier);
        }

        public float3 SampleNormal(
            float wx,
            float wz)
        {
            float hL =
                SampleHeightBilinear(
                    wx - scale,
                    wz);

            float hR =
                SampleHeightBilinear(
                    wx + scale,
                    wz);

            float hD =
                SampleHeightBilinear(
                    wx,
                    wz - scale);

            float hU =
                SampleHeightBilinear(
                    wx,
                    wz + scale);

            return math.normalize(
                new float3(
                    hL - hR,
                    scale * 2f,
                    hD - hU));
        }
        public float SampleHeightBilinear(float wx, float wz)
        {
            float gx = wx / scale;
            float gz = wz / scale;

            int chunkX = Mathf.FloorToInt(gx / mapChunkSize);
            int chunkZ = Mathf.FloorToInt(gz / mapChunkSize);

            float lxf = gx - chunkX * mapChunkSize;
            float lzf = gz - chunkZ * mapChunkSize;

            int x0 = Mathf.FloorToInt(lxf);
            int z0 = Mathf.FloorToInt(lzf);

            float tx = lxf - x0;
            float tz = lzf - z0;

            return math.lerp(
                math.lerp(SampleCell(chunkX, chunkZ, x0,     z0),
                        SampleCell(chunkX, chunkZ, x0 + 1, z0),     tx),
                math.lerp(SampleCell(chunkX, chunkZ, x0,     z0 + 1),
                        SampleCell(chunkX, chunkZ, x0 + 1, z0 + 1), tx),
                tz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SampleCell(int cx, int cz, int lx, int lz)
        {
            if (lx >= mapChunkSize) { lx -= mapChunkSize; cx++; }
            if (lz >= mapChunkSize) { lz -= mapChunkSize; cz++; }
            return GetMapData(cx, cz).HeightRaw[lz * mapChunkSize + lx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(int x, int y) => y * mapChunkSize + x;

        public bool TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells = default)
        {
            long chunk = GetPosition2Int(worldPos);
            ChunkData cd = GetMapData(chunk);

            int primaryIdx = WorldPosToLocalIdx(worldPos, chunk);

            int secCount = additionalWorldCells.Length;
            if (secCount > ObjectInstInfo.MaxSecondary)
            {
                Debug.LogWarning($"[MapGenerator] Too many secondary cells: {secCount} > {ObjectInstInfo.MaxSecondary}");
                return false;
            }

            Span<int> additionalLocalIdxs = stackalloc int[ObjectInstInfo.MaxSecondary];

            for (int i = 0; i < secCount; i++)
            {
                long worldCell = Position2Int.Pack(additionalWorldCells[i]);
                long cellChunk = GetPosition2Int(worldCell);

                if (cellChunk != chunk)
                {
                    Debug.LogWarning(
                        $"[MapGenerator] AdditionalCell {additionalWorldCells[i]} falls outside " +
                        $"primary chunk {chunk}. Cross-chunk multi-cell objects are not supported.");
                    return false;
                }

                additionalLocalIdxs[i] = WorldPosToLocalIdx(worldCell, chunk);
            }

            var info = new ObjectInstInfo(realPos, prefabID, amount);
            return cd.TryAddObject(primaryIdx, in info, additionalLocalIdxs[..secCount]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WorldPosToLocalIdx(long worldPos, long chunk)
        {
            int wx = Position2Int.GetX(worldPos);
            int wz = Position2Int.GetY(worldPos);
            int cx = Position2Int.GetX(chunk);
            int cz = Position2Int.GetY(chunk);

            int lx = Mathf.FloorToInt((wx - cx * generationSize) / (float)scale);
            int lz = Mathf.FloorToInt((wz - cz * generationSize) / (float)scale);

            return Idx(lx, lz);
        }
        public bool TryGetObject(long worldPos, out ObjectInstInfo info)
        {
            long chunk = GetPosition2Int(worldPos);
            long local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
            {
                info = default;
                return false;
            }

            return cd.TryGetObject(Idx(Position2Int.GetX(local), Position2Int.GetY(local)), out info);
        }
        public bool RemoveObject(long worldPos)
        {
            long chunk = GetPosition2Int(worldPos);
            long local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            return cd.RemoveObject(Idx(Position2Int.GetX(local), Position2Int.GetY(local)));
        }
        public bool ContainsObject(long worldPos)
        {
            long chunk = GetPosition2Int(worldPos);
            long local = GetLocalPosition(worldPos);

            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            return cd.Occupancy[Idx(Position2Int.GetX(local), Position2Int.GetY(local))] != 0;
        }

        public bool TryToAddPositionToChunk(long worldPos)
        {
            if (ContainsObject(worldPos))
                return false;

            long chunk = GetPosition2Int(worldPos);
            long local = GetLocalPosition(worldPos);
            ChunkData cd = GetMapData(chunk);

            var placeholder = new ObjectInstInfo(
                GetRealPosition(worldPos), -1, 0);

            return cd.TryAddObject(Idx(Position2Int.GetX(local), Position2Int.GetY(local)), in placeholder);
        }
        public void UnloadChunk(long position)
        {
            if (mapData.TryGetValue(position, out ChunkData cd))
            {
                cd.Dispose();
                mapData.Remove(position);
            }
        }
        public Transform terrainTransform, waterTransform;
        public MeshFilter terrainFilter;
        private int seed;
        public int Seed { get => seed; private set => seed = value; }

        private ObjectSystem objectSystem;
        private NAL_PC nal;

        [SerializeField] private Transform viewer;
        [SerializeField] private Vector3 viewerOffset;
        [SerializeField] private GrassSystem grassSystem;

        public ChunkGenerator chunkGenerator;
        public Vector3 waterOffset;

        private IEndless[] endless;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);

            seed = 16;
            mapData = new Dictionary<long, ChunkData>(64);

            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            chunkGenerator = new ChunkGenerator(chunkGenerationSettings, seed);

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
                    long centre = Position2Int.Pack(i, j);
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

        private long oldPlayerPosition, playerPosition;
        public UnityAction<long> onUpdate;

        private async UniTaskVoid GenerationUpdate()
        {
            playerPosition = GetPlayerPosition();

            for (int i = 0; i < 3; i++)
            {
                if (!chunkGenerationSettings.endlessFlag[i]) continue;
                endless[i].UpdateChunk(playerPosition);
                await UniTask.WaitForFixedUpdate();
            }
            
            grassSystem.UpdateGrassPlacement();

            while (!_cts.Token.IsCancellationRequested)
            {
                playerPosition = GetPlayerPosition();

                if (playerPosition != oldPlayerPosition)
                {
                    oldPlayerPosition = playerPosition;

                    int extendedChunk = chunkScale + 1;
                    for (int yOff = -extendedChunk; yOff <= extendedChunk; yOff++)
                    for (int xOff = -extendedChunk; xOff <= extendedChunk; xOff++)
                    {
                        long cp = Position2Int.Offset(playerPosition, xOff, yOff);
                        if (!mapData.ContainsKey(cp))
                            mapData[cp] = chunkGenerator.GenerateMapData(cp);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        if (!chunkGenerationSettings.endlessFlag[i]) continue;
                        endless[i].UpdateChunk(playerPosition);
                        await UniTask.WaitForFixedUpdate();
                    }

                    onUpdate?.Invoke(playerPosition);

                    grassSystem.UpdateGrassPlacement();
                }

                await UniTask.Delay(1000, cancellationToken: _cts.Token);
            }
        }

        public void ExtraUpdate()
        {
            playerPosition = GetPlayerPosition();
            for (int i = 0; i < 3; i++)
            {
                if (!chunkGenerationSettings.endlessFlag[i]) continue;
                endless[i].UpdateChunk(playerPosition);
            }
        }

        private long GetPlayerPosition()
        {
            if (viewer == null) return 0;
            
            int currentX = Mathf.RoundToInt((viewer.position.x - generationSize / 2f + viewerOffset.x) / generationSize);
            int currentZ = Mathf.RoundToInt((viewer.position.z + generationSize / 2f + viewerOffset.z) / generationSize);

            return Position2Int.Pack(currentX, currentZ);
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            foreach (var cd in mapData.Values) cd.Dispose();
            mapData.Clear();

            _cts.Cancel();
            chunkGenerator.Dispose();
        }
    }

    public sealed class ChunkData : IDisposable
    {
        private const int Size       = MapGenerator.mapChunkSize;
        public  const int TotalCells = Size * Size;

        public NativeArray<float>          HeightRaw;
        public readonly NativeArray<int>   BiomeMap;
        public NativeArray<int>            Occupancy;
        public NativeList<ObjectInstInfo>  Objects;
        public NativeArray<byte>           MoveCost;
        public List<StructureSpawnPoint> StructureSpawnPoints;

        public bool IsDirty { get; private set; }

        public ChunkData()
        {
            HeightRaw      = new NativeArray<float> (TotalCells, Allocator.Persistent);
            BiomeMap       = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            Occupancy      = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            MoveCost       = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            Objects        = new NativeList<ObjectInstInfo>(16, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs = default)
        {
            if ((uint)primaryIdx >= TotalCells) return false;
            if (Occupancy[primaryIdx] != 0)    return false;

            int secCount = additionalIdxs.Length;
            if (secCount > ObjectInstInfo.MaxSecondary) return false;

            for (int i = 0; i < secCount; i++)
            {
                int ac = additionalIdxs[i];
                if ((uint)ac >= TotalCells) return false;
                if (Occupancy[ac] != 0)    return false;
            }

            ObjectInstInfo stored = info;
            stored.PrimaryIdx     = primaryIdx;
            stored.SecondaryCount = secCount;

            unsafe
            {
                for (int i = 0; i < secCount; i++)
                    stored.SecondaryIdxs[i] = additionalIdxs[i];
            }

            int handle = Objects.Length + 1;
            Objects.Add(stored);
            Occupancy[primaryIdx] = handle;

            for (int i = 0; i < secCount; i++)
                Occupancy[additionalIdxs[i]] = -handle;

            IsDirty = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetObject(int cellIdx, out ObjectInstInfo info)
        {
            info = default;
            if ((uint)cellIdx >= TotalCells) return false;

            int h = Occupancy[cellIdx];
            if (h == 0) return false;

            int objIdx = (h > 0 ? h : -h) - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            info = Objects[objIdx];
            return true;
        }
        public bool RemoveObject(int cellIdx)
        {
            if ((uint)cellIdx >= TotalCells) return false;

            int h = Occupancy[cellIdx];
            if (h == 0) return false;

            int primaryHandle = h > 0 ? h : -h;
            int objIdx        = primaryHandle - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            ObjectInstInfo obj = Objects[objIdx];

            Occupancy[obj.PrimaryIdx] = 0;
            unsafe
            {
                for (int i = 0; i < obj.SecondaryCount; i++)
                    Occupancy[obj.SecondaryIdxs[i]] = 0;
            }

            int last = Objects.Length - 1;
            if (objIdx != last)
            {
                ObjectInstInfo tail = Objects[last];
                int newH = objIdx + 1;

                Occupancy[tail.PrimaryIdx] = newH;
                unsafe
                {
                    for (int i = 0; i < tail.SecondaryCount; i++)
                        Occupancy[tail.SecondaryIdxs[i]] = -newH;
                }

                Objects[objIdx] = tail;
            }

            Objects.RemoveAt(last);
            IsDirty = true;
            return true;
        }

        public void ClearDirty() => IsDirty = false;

        public void Dispose()
        {
            if (HeightRaw.IsCreated)      HeightRaw.Dispose();
            if (BiomeMap.IsCreated)       BiomeMap.Dispose();
            if (Occupancy.IsCreated)      Occupancy.Dispose();
            if (MoveCost.IsCreated)       MoveCost.Dispose();
            if (Objects.IsCreated)        Objects.Dispose();
        }
    }

    public readonly struct StructureSpawnPoint
    {
        public readonly long WorldPosition;
        public readonly GenerationSettingsSO WfcSettings;
        public readonly TilePatternSO InitialPattern;

        public StructureSpawnPoint(
            long pos,
            GenerationSettingsSO settings,
            TilePatternSO pattern)
        {
            WorldPosition  = pos;
            WfcSettings    = settings;
            InitialPattern = pattern;
        }
    }

    public unsafe struct ObjectInstInfo
    {
        public int     PrefabID;
        public Vector3 Position;
        public int     Amount;
        public int     PrimaryIdx;
        public int     SecondaryCount;
        public fixed int SecondaryIdxs[8];

        public const int MaxSecondary = 8;

        public ObjectInstInfo(Vector3 pos, int prefab, int amount)
        {
            Position       = pos;
            PrefabID       = prefab;
            Amount         = amount;
            PrimaryIdx     = -1;
            SecondaryCount = 0;
        }
    }
}