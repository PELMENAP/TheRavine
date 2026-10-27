using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using System.Threading;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;
using TheRavine.Events;
using R3;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    using TheRavine.Base;
    using Unity.Mathematics;

    public class MapGenerator : MonoBehaviour, ISetAble, IWorldCommandTarget
    {
        [SerializeField] private ChunkGenerationSettings chunkGenerationSettings;
        private readonly CancellationTokenSource _cts = new();

        public const int mapChunkSize = 64, chunkScale = 1, scale = 2;
        public const int chunkSize = scale * mapChunkSize;
        public const int generationSize = scale * mapChunkSize * (1 + 2 * chunkScale);
        public const float maxTerrainHeight = 100f;

        public const int CellShift = 1;
        public const int RowShift = 6;
        public const int ChunkShift = CellShift + RowShift;
        public const int RowMask = mapChunkSize - 1;
        public const int WindowSide = 2 * chunkScale + 1;
        public const float InvScale = 1f / scale;
        public const float InvChunkSize = 1f / chunkSize;

        private const int ShiftCheck = 1 / ((1 << CellShift) == scale && (1 << RowShift) == mapChunkSize && (1 << ChunkShift) == chunkSize ? 1 : 0);

        private LongDictionary<ChunkData> mapData = new();
        private readonly Stack<ChunkData> chunkPool = new(64);
        private long[] evictBuffer = new long[256];
        private long[] dirtyKeys = new long[16];
        private int dirtyCount;

        private long sampleKey = long.MinValue;
        private ChunkData sampleChunk;

        private long windowCenter;
        private bool hasWindow, ready, disposed;
        private int facing = 1;
        private int frame;
        private int lodRingCount;
        private bool evictionPending;
        private float nextEvictionScan;
        private long budgetTicks;

        private IDisposable playerSubscription;
        private AEntity boundPlayer;
        private EventBus playerEventBus;

        public ChunkGenerationSettings Settings => chunkGenerationSettings;
        public long WindowCenter => windowCenter;
        public int Facing => facing;
        public int LodRingCount => lodRingCount;
        public int LoadedChunkCount => mapData.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WorldToChunk(long worldPos) =>
            Position2Int.Pack(Position2Int.GetX(worldPos) >> ChunkShift, Position2Int.GetY(worldPos) >> ChunkShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldToLocalIdx(long worldPos) =>
            CellToLocalIdx(Position2Int.GetX(worldPos) >> CellShift, Position2Int.GetY(worldPos) >> CellShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CellToChunk(int cellX, int cellZ) =>
            Position2Int.Pack(cellX >> RowShift, cellZ >> RowShift);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CellToLocalIdx(int cellX, int cellZ) =>
            ((cellZ & RowMask) << RowShift) | (cellX & RowMask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkCoord(float world) => (int)math.floor(world * InvChunkSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WindowChunk(long center, int gx, int gz) =>
            Position2Int.Offset(center, gx - chunkScale, gz - chunkScale);

        public static Vector3 WindowOriginWorld(long center) => new(
            (Position2Int.GetX(center) - chunkScale) * chunkSize,
            0f,
            (Position2Int.GetY(center) - chunkScale) * chunkSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInWindow(long center, long chunk, int radius) =>
            math.abs(Position2Int.GetX(chunk) - Position2Int.GetX(center)) <= radius &&
            math.abs(Position2Int.GetY(chunk) - Position2Int.GetY(center)) <= radius;

        public bool IsInActiveWindow(long worldPos) =>
            hasWindow && IsInWindow(windowCenter, WorldToChunk(worldPos), chunkScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(long chunkKey) => EnsureChunk(chunkKey, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkData GetMapData(int x, int y) => EnsureChunk(Position2Int.Pack(x, y), 0);

        public ChunkData EnsureChunk(long chunkKey, int ring)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData data))
            {
                if (data.DetailLevel > ring)
                    chunkGenerator.RaiseDetail(data, chunkKey, ring);
            }
            else
            {
                data = chunkPool.Count > 0 ? chunkPool.Pop() : new ChunkData();
                chunkGenerator.Generate(data, chunkKey, ring);
                chunkStore?.TryRestore(chunkKey, data);
                mapData[chunkKey] = data;
            }

            data.LastAccessFrame = frame;
            return data;
        }

        public bool EnsureRegion(long center, int radius, int ring, long deadline)
        {
            bool complete = true;
            int cx = Position2Int.GetX(center);
            int cz = Position2Int.GetY(center);

            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                long key = Position2Int.Pack(cx + dx, cz + dz);
                if (mapData.TryGetValue(key, out ChunkData cd) && cd.DetailLevel <= ring)
                {
                    cd.LastAccessFrame = frame;
                    continue;
                }

                if (FrameBudget.Expired(deadline))
                {
                    complete = false;
                    continue;
                }

                EnsureChunk(key, ring);
            }

            return complete;
        }

        public bool TryBuildHeightWindow(long center, out HeightWindow window)
        {
            window = default;
            int ox = Position2Int.GetX(center) - HeightWindow.Radius;
            int oz = Position2Int.GetY(center) - HeightWindow.Radius;

            for (int z = 0; z < HeightWindow.Side; z++)
            for (int x = 0; x < HeightWindow.Side; x++)
            {
                if (!mapData.TryGetValue(Position2Int.Pack(ox + x, oz + z), out ChunkData cd))
                    return false;
                window.Set(x, z, cd);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasChunk(long chunkKey) => mapData.ContainsKey(chunkKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetChunk(long chunkKey, out ChunkData data) =>
            mapData.TryGetValue(chunkKey, out data);

        public void RemoveChunk(long chunkKey) => UnloadChunk(chunkKey);

        public void UnloadChunk(long chunkKey)
        {
            if (mapData.TryGetValue(chunkKey, out ChunkData cd))
                ReleaseChunk(chunkKey, cd);
        }

        private void ReleaseChunk(long chunkKey, ChunkData cd)
        {
            nal?.RemoveChunk(chunkKey);
            mapData.Remove(chunkKey);

            if (ReferenceEquals(sampleChunk, cd))
            {
                sampleChunk = null;
                sampleKey = long.MinValue;
            }

            if (chunkPool.Count >= chunkGenerationSettings.chunkPoolCapacity)
            {
                cd.Dispose();
                return;
            }

            cd.ResetForReuse();
            chunkPool.Push(cd);
        }

        public bool IsHeightIsLiveAble(long position) =>
            GetRealPosition(position).y > 5;

        public bool IsWaterHeight(long position) =>
            GetRealPosition(position).y < 5;

        public Vector3 GetRealPosition(long pos) => new(
            Position2Int.GetX(pos),
            GetMapData(WorldToChunk(pos)).HeightRaw[WorldToLocalIdx(pos)],
            Position2Int.GetY(pos));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSpeedModifier(float x, float z, float2 moveDirection)
            => SpeedFromNormal(SampleNormal(x, z), moveDirection);

        public static float SpeedFromNormal(float3 normal, float2 moveDirection)
        {
            float slopeAngle = math.degrees(math.acos(math.clamp(normal.y, -1f, 1f)));
            float2 uphill    = math.normalizesafe(new float2(-normal.x, -normal.z));
            float uphillDot  = math.dot(math.normalize(moveDirection), uphill);

            if (uphillDot > 0f && slopeAngle > 70f) return 0f;

            float modifier = 1f - slopeAngle / 70f;
            if (uphillDot < 0f)
                modifier = math.lerp(modifier, 1f, -uphillDot * 0.25f);

            return math.saturate(modifier);
        }

        public float3 SampleNormal(float wx, float wz)
            => NormalFromHeights(
                SampleHeightBilinear(wx - scale, wz),
                SampleHeightBilinear(wx + scale, wz),
                SampleHeightBilinear(wx, wz - scale),
                SampleHeightBilinear(wx, wz + scale));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalFromHeights(float hL, float hR, float hD, float hU)
            => math.normalize(new float3(hL - hR, scale * 2f, hD - hU));

        public float SampleHeightBilinear(float wx, float wz)
        {
            TrySampleHeightBilinear(wx, wz, out float height);
            return height;
        }

        public bool TrySampleHeightBilinear(float wx, float wz, out float height)
        {
            float gx = wx * InvScale;
            float gz = wz * InvScale;
            float fx = math.floor(gx);
            float fz = math.floor(gz);
            int x0 = (int)fx;
            int z0 = (int)fz;

            if (!TryCellHeight(x0, z0, out float h00))
            {
                height = 0f;
                return false;
            }

            float h10 = TryCellHeight(x0 + 1, z0,     out float a) ? a : h00;
            float h01 = TryCellHeight(x0,     z0 + 1, out float b) ? b : h00;
            float h11 = TryCellHeight(x0 + 1, z0 + 1, out float c) ? c : h00;

            float tx = gx - fx;
            float tz = gz - fz;

            height = math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryCellHeight(int cellX, int cellZ, out float height)
        {
            long key = CellToChunk(cellX, cellZ);
            if (key != sampleKey)
            {
                if (!mapData.TryGetValue(key, out ChunkData cd))
                {
                    height = 0f;
                    return false;
                }
                sampleKey = key;
                sampleChunk = cd;
            }

            height = sampleChunk.HeightRaw[CellToLocalIdx(cellX, cellZ)];
            return true;
        }

        public bool TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells = default,
            int levels = 1) =>
            TryAddObjectEx(worldPos, realPos, prefabID, amount, additionalWorldCells, levels) == PlacementResult.Ok;

        bool IWorldCommandTarget.TryAddObject(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells,
            int levels)
        {
            ObjectInfo info = additionalWorldCells.IsEmpty && objectSystem != null ? objectSystem.GetInfo(prefabID) : null;
            return info != null
                ? TryAddFromInfo(worldPos, realPos, info, amount, levels) == PlacementResult.Ok
                : TryAddObjectEx(worldPos, realPos, prefabID, amount, additionalWorldCells, levels) == PlacementResult.Ok;
        }

        public PlacementResult TryAddFromInfo(long worldPos, Vector3 realPos, ObjectInfo info, int amount, int levels)
        {
            Span<Vector2Int> cells = stackalloc Vector2Int[ObjectInstInfo.MaxSecondary];
            int count = Footprint.Resolve(info, Position2Int.UnpackToVector(worldPos), cells);
            if (count > cells.Length)
            {
                RavineLog.Warning($"[MapGenerator] Too many secondary cells: {count} > {ObjectInstInfo.MaxSecondary}");
                return PlacementResult.TooManyCells;
            }

            return TryAddObjectEx(worldPos, realPos, info.Id, amount, cells[..count], levels);
        }

        public PlacementResult TryAddObjectEx(
            long worldPos,
            Vector3 realPos,
            int prefabID,
            int amount,
            ReadOnlySpan<Vector2Int> additionalWorldCells = default,
            int levels = 1)
        {
            long chunk = WorldToChunk(worldPos);
            ChunkData cd = GetMapData(chunk);
            if (cd == null) return PlacementResult.NoChunk;

            int primaryIdx = WorldToLocalIdx(worldPos);

            int secCount = additionalWorldCells.Length;
            if (secCount > ObjectInstInfo.MaxSecondary)
            {
                RavineLog.Warning($"[MapGenerator] Too many secondary cells: {secCount} > {ObjectInstInfo.MaxSecondary}");
                return PlacementResult.TooManyCells;
            }

            Span<int> additionalLocalIdxs = stackalloc int[ObjectInstInfo.MaxSecondary];
            int unique = 0;

            for (int i = 0; i < secCount; i++)
            {
                long worldCell = Position2Int.Pack(additionalWorldCells[i]);

                if (WorldToChunk(worldCell) != chunk)
                {
                    RavineLog.Warning(
                        $"[MapGenerator] AdditionalCell {additionalWorldCells[i]} falls outside " +
                        $"primary chunk {chunk}. Cross-chunk multi-cell objects are not supported.");
                    return PlacementResult.CrossChunk;
                }

                int local = WorldToLocalIdx(worldCell);
                if (local == primaryIdx || additionalLocalIdxs[..unique].IndexOf(local) >= 0) continue;
                additionalLocalIdxs[unique++] = local;
            }

            var info = new ObjectInstInfo(realPos, prefabID, amount);
            if (!cd.TryAddObject(primaryIdx, in info, additionalLocalIdxs[..unique], levels, out PlacementResult result))
                return result;

            MarkChunkDirty(chunk);
            return PlacementResult.Ok;
        }

        public bool TryGetObject(long worldPos, out ObjectInstInfo info)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
            {
                info = default;
                return false;
            }

            return cd.TryGetObject(WorldToLocalIdx(worldPos), out info);
        }

        public bool RemoveObject(long worldPos)
        {
            long chunk = WorldToChunk(worldPos);
            if (!mapData.TryGetValue(chunk, out ChunkData cd))
                return false;

            if (!cd.RemoveObject(WorldToLocalIdx(worldPos)))
                return false;

            MarkChunkDirty(chunk);
            return true;
        }

        public bool ContainsObject(long worldPos)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
                return false;

            return cd.Occupancy[WorldToLocalIdx(worldPos)] != 0;
        }

        public bool IsOccupiedAt(long worldPos, int level)
        {
            if (!mapData.TryGetValue(WorldToChunk(worldPos), out ChunkData cd))
                return false;

            return cd.HasLevelAt(WorldToLocalIdx(worldPos), level);
        }

        public bool TryToAddPositionToChunk(long worldPos)
        {
            if (ContainsObject(worldPos))
                return false;

            long chunk = WorldToChunk(worldPos);
            ChunkData cd = GetMapData(chunk);

            var placeholder = new ObjectInstInfo(
                GetRealPosition(worldPos), ObjectInstInfo.PlaceholderId, 0);

            if (!cd.TryAddObject(WorldToLocalIdx(worldPos), in placeholder))
                return false;

            MarkChunkDirty(chunk);
            return true;
        }

        public void MarkChunkDirty(long chunkKey)
        {
            for (int i = 0; i < dirtyCount; i++)
                if (dirtyKeys[i] == chunkKey) return;

            if (dirtyCount == dirtyKeys.Length)
                Array.Resize(ref dirtyKeys, dirtyCount * 2);

            dirtyKeys[dirtyCount++] = chunkKey;
        }

        public Transform terrainTransform, waterTransform;
        public MeshFilter terrainFilter;
        private int seed;
        public int Seed { get => seed; private set => seed = value; }
        private const int DefaultSeed = 16;

        private ObjectSystem objectSystem;
        private NAL_PC nal;
        private ChunkStore chunkStore;
        private IDisposable autosaveSubscription;
        private WorldRegistry boundRegistry;
        private string boundWorldId;

        [SerializeField] private Transform viewer;
        [SerializeField] private Vector3 viewerOffset;
        [SerializeField] private GrassSystem grassSystem;
        [SerializeField] private Texture2DArray biomeAlbedo;
        [SerializeField, Min(0.1f)] private float nalApplyBudgetMs = 1f;
        [SerializeField, Min(0f)] private float nalCatchUpMaxSeconds = 600f;
        [SerializeField, Min(0)] private int nalCatchUpMaxSteps = 8;
        [SerializeField, Min(0.01f)] private float nalGrowStageSeconds = 20f;
        [SerializeField, Min(16)] private int nalStoreCapacity = 8192;
        [SerializeField, Min(1)] private int nalMaxDecisionsPerTick = 256;
        [SerializeField, Min(0f)] private float nalSecondsPerDelayUnit = 0.01f;

        public int NalMaxDecisionsPerTick => nalMaxDecisionsPerTick;
        public float NalSecondsPerDelayUnit => nalSecondsPerDelayUnit;
        public float NalCatchUpMaxSeconds => nalCatchUpMaxSeconds;
        public int NalCatchUpMaxSteps => nalCatchUpMaxSteps;
        public float NalGrowStageSeconds => nalGrowStageSeconds;

        public WorldCommandBuffer Commands { get; private set; }
        public WorldCommandStats CommandStats { get; } = new();
        public WorldCommandStats ImmediateStats { get; } = new();

        public ChunkGenerator chunkGenerator;
        public Vector3 waterOffset;

        private IEndless[] endless;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Register(this);

            ServiceLocator.Services.TryGet(out WorldRegistry worldRegistry);
            bool worldLoaded = worldRegistry != null && worldRegistry.HasLoadedWorld;
            seed = worldLoaded ? worldRegistry.GetCurrentState().seed : DefaultSeed;
            mapData = new LongDictionary<ChunkData>(256);
            budgetTicks = FrameBudget.TicksFromMs(chunkGenerationSettings.frameBudgetMs);

            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            chunkGenerator = new ChunkGenerator(chunkGenerationSettings, seed);
            if (worldLoaded) BindChunkStore(worldRegistry);

            CurvedWorld.Configure(chunkGenerationSettings.curveRadius, chunkGenerationSettings.curveFlat, chunkGenerationSettings.curveFocusDistance);
            CurvedWorld.SetFacing(facing);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            BiomeShaderGlobals.Apply(chunkGenerationSettings, biomeAlbedo);

            BindFirstPlayer();

            if (worldLoaded)
            {
                var config = worldRegistry.GetCurrentConfig();
                chunkGenerationSettings.isRiver = config.generateRivers;
            }

            if (chunkStore != null && ServiceLocator.Services.TryGet(out AutosaveCoordinator autosave))
                autosaveSubscription = autosave.SubscribeBeforeSave(SaveModifiedChunks);

            Commands = new WorldCommandBuffer(256);

            if (chunkGenerationSettings.endlessFlag[3])
            {
                nal = new NAL_PC(this, objectSystem, _cts.Token, nalStoreCapacity);
                nal.RunNAL().Forget();
            }

            SetupEndless();
            ready = true;
            callback?.Invoke();
        }

        private void SetupEndless()
        {
            bool terrain = chunkGenerationSettings.endlessFlag[0];
            int[] steps = chunkGenerationSettings.lodSteps;
            lodRingCount = terrain ? math.min(math.min(chunkGenerationSettings.maxLodRing, steps?.Length ?? 0), HorizonRingCount()) : 0;

            endless = new IEndless[4];
            if (terrain)
                endless[0] = new EndlessTerrain(this, chunkGenerationSettings);
            if (chunkGenerationSettings.endlessFlag[1])
                endless[1] = new EndlessLiquids(this);
            if (chunkGenerationSettings.endlessFlag[2])
            {
                endless[2] = new EndlessObjects(this, objectSystem);
                if (lodRingCount > 0)
                    endless[3] = new FarObjectRenderer(this, objectSystem, chunkGenerationSettings);
            }
        }

        private int HorizonRingCount()
        {
            float distance = CurvedWorld.VisibleDistance(
                chunkGenerationSettings.horizonCameraHeight,
                chunkGenerationSettings.horizonCameraBack,
                chunkGenerationSettings.horizonPeakHeight);

            if (float.IsInfinity(distance)) return int.MaxValue;

            float beyondWindow = distance - (chunkScale + 0.5f) * chunkSize;
            return math.max(1, (int)math.ceil(beyondWindow / (WindowSide * chunkSize)));
        }

        private void BindFirstPlayer()
        {
            var players = ServiceLocator.Players.GetAllPlayers();
            if (players.Count > 0)
            {
                BindPlayer(players[0]);
                return;
            }

            playerSubscription = ServiceLocator.Players.OnPlayerRegistered.Take(1).Subscribe(BindPlayer);
        }

        private void BindPlayer(AEntity player)
        {
            if (player == null || boundPlayer != null) return;
            boundPlayer = player;

            TransformComponent transformComponent = player.GetEntityComponent<TransformComponent>();
            if (transformComponent != null)
                viewer = transformComponent.GetEntityTransform();

            playerEventBus = player.GetEntityComponent<EventBusComponent>()?.EventBus;
            playerEventBus?.Subscribe<CameraPlace>(OnCameraPlace);
        }

        private void OnCameraPlace(AEntity sender, CameraPlace e)
        {
            int newFacing = e.flip ? -1 : 1;
            if (newFacing == facing) return;

            facing = newFacing;
            CurvedWorld.SetFacing(facing);
            if (hasWindow && grassSystem != null)
                grassSystem.UpdateGrassPlacement(this, windowCenter);
            if (endless != null)
            {
                for (int i = 0; i < endless.Length; i++)
                    if (endless[i] is IViewDirectional directional)
                        directional.SetFacing(facing);
            }
            evictionPending = true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            {
                CurvedWorld.PushFlat();
                return;
            }

            Vector3 position = camera.transform.position;
            float originZ = position.z + facing * CurvedWorld.FocusDistance;

            if (!TrySampleHeightBilinear(position.x, originZ, out float referenceHeight))
                referenceHeight = position.y;

            CurvedWorld.Push(originZ, referenceHeight);
        }

        public bool TryGetViewerChunk(out int x, out int z)
        {
            x = Position2Int.GetX(windowCenter);
            z = Position2Int.GetY(windowCenter);
            return hasWindow && viewer != null;
        }

        public void ClearNALQueue() => nal?.Clear();
        public void BeginNALChunk(long chunkKey) => nal?.BeginChunk(chunkKey);
        public void EndNALChunk() => nal?.EndChunk();
        public void RemoveNALChunk(long chunkKey) => nal?.RemoveChunk(chunkKey);

        public bool ApplyImmediate(in WorldCommand command) =>
            WorldCommandBuffer.ApplyOne(this, in command, ImmediateStats);

        private void ApplyWorldCommands()
        {
            if (Commands == null || Commands.Count == 0) return;

            int pendingBefore = Commands.Count;
            long start = FrameBudget.Now;
            Commands.Apply(this, start + FrameBudget.TicksFromMs(nalApplyBudgetMs), CommandStats);
            int applyMs = (int)((FrameBudget.Now - start) * 1000 / System.Diagnostics.Stopwatch.Frequency);

            nal?.OnCommandsApplied(Commands.AppliedAdds, CommandStats, applyMs, pendingBefore, Commands.Count);
            Commands.AppliedAdds.Clear();

            ExtraUpdate();
        }
        public void AddNALObject(Vector2Int pos, int prefabId) => nal?.Enqueue(pos, prefabId);

        public UnityAction<long> onUpdate;

        private void Update()
        {
            if (!ready) return;

            frame = Time.frameCount;
            long deadline = FrameBudget.Now + budgetTicks;

            long center = ComputeWindowCenter();
            if (!hasWindow || center != windowCenter)
                MoveWindow(center);

            ApplyWorldCommands();
            FlushDirty();

            for (int i = 0; i < endless.Length; i++)
                endless[i]?.Tick(deadline);

            if (evictionPending || Time.unscaledTime >= nextEvictionScan)
                EvictChunks();
        }

        private void MoveWindow(long center)
        {
            windowCenter = center;
            hasWindow = true;

            EnsureRegion(center, chunkScale, 0, FrameBudget.Unlimited);
            EnsureRegion(center, HeightWindow.Radius, 1, FrameBudget.Unlimited);

            for (int i = 0; i < endless.Length; i++)
                endless[i]?.UpdateChunk(center);

            onUpdate?.Invoke(center);

            if (grassSystem != null)
                grassSystem.UpdateGrassPlacement(this, center);

            evictionPending = true;
        }

        public void ExtraUpdate() => FlushDirty();

        private void FlushDirty()
        {
            if (dirtyCount == 0 || endless == null) return;

            for (int i = 0; i < dirtyCount; i++)
            for (int e = 0; e < endless.Length; e++)
                endless[e]?.OnChunkDirty(dirtyKeys[i]);

            dirtyCount = 0;
        }

        private long ComputeWindowCenter()
        {
            if (viewer == null) return hasWindow ? windowCenter : 0L;

            Vector3 p = viewer.position + viewerOffset;
            return Position2Int.Pack(ChunkCoord(p.x), ChunkCoord(p.z));
        }

        private void BindChunkStore(WorldRegistry registry)
        {
            boundRegistry = registry;
            boundWorldId = registry.GetCurrentWorldId();
            chunkStore = ChunkStore.ForWorld(boundWorldId, seed);
            registry.WorldRenamed += OnWorldRenamed;
        }

        private void OnWorldRenamed(string oldId, string newId)
        {
            if (chunkStore == null || oldId != boundWorldId) return;

            boundWorldId = newId;
            chunkStore.Rebind(newId);
        }

        private void SaveModifiedChunks()
        {
            if (chunkStore == null) return;

            foreach (var kv in mapData)
            {
                ChunkData cd = kv.Value;
                if (cd.NeedsSave && chunkStore.TrySave(kv.Key, cd))
                    cd.MarkPersisted();
            }
        }

        private void EvictChunks()
        {
            evictionPending = false;
            nextEvictionScan = Time.unscaledTime + chunkGenerationSettings.evictionScanInterval;
            if (!hasWindow) return;

            int idleFrames = chunkGenerationSettings.evictionIdleFrames;
            int n = 0;

            foreach (var kv in mapData)
            {
                ChunkData cd = kv.Value;
                if (cd.NeedsSave || frame - cd.LastAccessFrame < idleFrames || IsRetained(kv.Key))
                    continue;

                if (n == evictBuffer.Length)
                    Array.Resize(ref evictBuffer, n * 2);
                evictBuffer[n++] = kv.Key;
            }

            for (int i = 0; i < n; i++)
            {
                if (mapData.TryGetValue(evictBuffer[i], out ChunkData cd))
                    ReleaseChunk(evictBuffer[i], cd);
            }
        }

        private bool IsRetained(long chunkKey)
        {
            int hysteresis = chunkGenerationSettings.evictionHysteresis;
            int dx = math.abs(Position2Int.GetX(chunkKey) - Position2Int.GetX(windowCenter));
            int dz = Position2Int.GetY(chunkKey) - Position2Int.GetY(windowCenter);

            int near = math.max(chunkGenerationSettings.retainRadius, HeightWindow.Radius) + hysteresis;
            if (dx <= near && math.abs(dz) <= near) return true;
            if (lodRingCount <= 0) return false;

            int forward = dz * facing;
            int reach = HeightWindow.Radius + hysteresis;
            if (forward < 0 || forward > lodRingCount * WindowSide + reach) return false;

            int ring = math.min((forward + reach) / WindowSide, lodRingCount);
            return ring > 0 && dx <= ring * WindowSide + reach;
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            if (disposed) return;
            disposed = true;
            ready = false;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            CurvedWorld.PushFlat();

            playerSubscription?.Dispose();
            playerSubscription = null;
            playerEventBus?.Unsubscribe<CameraPlace>(OnCameraPlace);
            playerEventBus = null;
            boundPlayer = null;

            autosaveSubscription?.Dispose();
            autosaveSubscription = null;
            if (boundRegistry != null) boundRegistry.WorldRenamed -= OnWorldRenamed;
            boundRegistry = null;
            SaveModifiedChunks();

            if (endless != null)
            {
                for (int i = 0; i < endless.Length; i++)
                    endless[i]?.Dispose();
            }

            foreach (var cd in mapData) cd.Value.Dispose();
            mapData.Clear();

            while (chunkPool.Count > 0)
                chunkPool.Pop().Dispose();

            sampleChunk = null;
            sampleKey = long.MinValue;

            _cts.Cancel();
            nal?.Dispose();
            nal = null;
            Commands?.Dispose();
            Commands = null;
            chunkGenerator?.Dispose();
        }
    }

    public sealed class ChunkData : IDisposable
    {
        private const int Size       = MapGenerator.mapChunkSize;
        public  const int TotalCells = Size * Size;
        public  const byte NotGenerated = byte.MaxValue;

        public NativeArray<float>          HeightRaw;
        public readonly NativeArray<int>   BiomeMap;
        public NativeArray<int>            Occupancy;
        public NativeArray<byte>           LevelMask;
        public NativeList<ObjectInstInfo>  Objects;
        private Dictionary<int, BuildingVolume> buildings;
        public NativeArray<byte>           MoveCost;
        public NativeArray<float2>         Climate;
        public NativeArray<float>          RiverBlend;
        public List<StructureSpawnPoint> StructureSpawnPoints;

        public bool IsDirty { get; private set; }
        public int Version { get; private set; }
        public int GeneratedVersion { get; private set; }
        public bool IsModified => Version != GeneratedVersion;
        public bool NeedsSave => IsModified && Version != SavedVersion;
        public int SavedVersion { get; private set; }
        public byte DetailLevel { get; internal set; } = NotGenerated;
        public int LastAccessFrame;

        public ChunkData()
        {
            HeightRaw      = new NativeArray<float> (TotalCells, Allocator.Persistent);
            BiomeMap       = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            Occupancy      = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            LevelMask      = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            MoveCost       = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            Climate        = new NativeArray<float2>(TotalCells, Allocator.Persistent);
            RiverBlend     = new NativeArray<float> (TotalCells, Allocator.Persistent);
            Objects        = new NativeList<ObjectInstInfo>(16, Allocator.Persistent);
        }

        public int BuildingCount => buildings != null ? buildings.Count : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs = default, int levels = 1)
            => TryAddObject(primaryIdx, in info, additionalIdxs, levels, out _);

        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs, int levels, out PlacementResult result)
        {
            result = Validate(primaryIdx, additionalIdxs, levels);
            if (result != PlacementResult.Ok) return false;

            int secCount = additionalIdxs.Length;
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

            if (levels > 1) AddVolume(primaryIdx, additionalIdxs, levels);

            IsDirty = true;
            Version++;
            return true;
        }

        private PlacementResult Validate(int primaryIdx, ReadOnlySpan<int> additionalIdxs, int levels)
        {
            if ((uint)primaryIdx >= TotalCells) return PlacementResult.OutOfBounds;
            if (Occupancy[primaryIdx] != 0)    return PlacementResult.Occupied;

            int secCount = additionalIdxs.Length;
            if (secCount > ObjectInstInfo.MaxSecondary) return PlacementResult.TooManyCells;

            for (int i = 0; i < secCount; i++)
            {
                int ac = additionalIdxs[i];
                if ((uint)ac >= TotalCells) return PlacementResult.OutOfBounds;
                if (Occupancy[ac] != 0 || ac == primaryIdx) return PlacementResult.Occupied;
                for (int j = 0; j < i; j++)
                    if (additionalIdxs[j] == ac) return PlacementResult.Occupied;
            }

            if (levels < 1 || levels > ObjectInfo.MaxLevels) return PlacementResult.InvalidLevels;
            if (!UpperLevelsFree(primaryIdx, additionalIdxs, levels)) return PlacementResult.UpperLevelOccupied;
            return PlacementResult.Ok;
        }

        private bool UpperLevelsFree(int primaryIdx, ReadOnlySpan<int> additionalIdxs, int levels)
        {
            for (int l = 1; l < levels; l++)
            {
                byte bit = (byte)(1 << l);
                if ((LevelMask[primaryIdx] & bit) != 0) return false;
                for (int i = 0; i < additionalIdxs.Length; i++)
                    if ((LevelMask[additionalIdxs[i]] & bit) != 0) return false;
            }
            return true;
        }

        private void AddVolume(int anchor, ReadOnlySpan<int> additionalIdxs, int levels)
        {
            var cells = new LevelCell[(1 + additionalIdxs.Length) * levels];
            int n = 0;

            for (int l = 0; l < levels; l++)
            {
                byte level = (byte)l;
                byte bit   = (byte)(1 << l);

                cells[n++] = new LevelCell(anchor, level);
                if (l > 0) SetLevelBit(anchor, bit);

                for (int i = 0; i < additionalIdxs.Length; i++)
                {
                    cells[n++] = new LevelCell(additionalIdxs[i], level);
                    if (l > 0) SetLevelBit(additionalIdxs[i], bit);
                }
            }

            buildings ??= new Dictionary<int, BuildingVolume>();
            buildings[anchor] = new BuildingVolume(anchor, levels, cells);
        }

        private void SetLevelBit(int cellIdx, byte bit) => LevelMask[cellIdx] = (byte)(LevelMask[cellIdx] | bit);

        private void ClearVolume(BuildingVolume volume)
        {
            for (int i = 0; i < volume.Cells.Length; i++)
            {
                LevelCell cell = volume.Cells[i];
                if (cell.Level == 0) continue;
                LevelMask[cell.LocalIdx] = (byte)(LevelMask[cell.LocalIdx] & ~(1 << cell.Level));
            }
        }

        public bool TryGetBuilding(int anchorIdx, out BuildingVolume volume)
        {
            volume = null;
            return buildings != null && buildings.TryGetValue(anchorIdx, out volume);
        }

        public bool HasLevelAt(int cellIdx, int level)
        {
            if ((uint)cellIdx >= TotalCells || (uint)level >= (uint)ObjectInfo.MaxLevels) return false;
            if (level == 0) return Occupancy[cellIdx] != 0;
            return (LevelMask[cellIdx] & (1 << level)) != 0;
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

            if (buildings != null && buildings.Remove(obj.PrimaryIdx, out BuildingVolume volume))
                ClearVolume(volume);

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
            Version++;
            return true;
        }

        public void ClearDirty() => IsDirty = false;

        public void ClearObjects()
        {
            unsafe
            {
                UnsafeUtility.MemClear(Occupancy.GetUnsafePtr(), (long)TotalCells * sizeof(int));
                UnsafeUtility.MemClear(LevelMask.GetUnsafePtr(), TotalCells);
            }

            Objects.Clear();
            buildings?.Clear();
            Version++;
            IsDirty = true;
        }

        public int LevelsAt(int anchorIdx) => TryGetBuilding(anchorIdx, out BuildingVolume volume) ? volume.Levels : 1;

        internal void MarkPersisted() => SavedVersion = Version;

        internal void CommitRestore(byte detailLevel)
        {
            DetailLevel = detailLevel;
            GeneratedVersion = Version;
            SavedVersion = Version;
        }

        internal void ResetForReuse()
        {
            ClearObjects();
            StructureSpawnPoints?.Clear();
            DetailLevel = NotGenerated;
            IsDirty = false;
            Version++;
            GeneratedVersion = Version;
        }

        internal void CommitGeneration(bool wasModified)
        {
            if (!wasModified) GeneratedVersion = Version;
        }

        public void Dispose()
        {
            if (HeightRaw.IsCreated)      HeightRaw.Dispose();
            if (BiomeMap.IsCreated)       BiomeMap.Dispose();
            if (Occupancy.IsCreated)      Occupancy.Dispose();
            if (LevelMask.IsCreated)      LevelMask.Dispose();
            if (MoveCost.IsCreated)       MoveCost.Dispose();
            if (Climate.IsCreated)        Climate.Dispose();
            if (RiverBlend.IsCreated)     RiverBlend.Dispose();
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
        public const int PlaceholderId = -1;
        public const int NoSpawnConfig = -1;
        public const int MaxSecondary = 8;

        public int     PrefabID;
        public Vector3 Position;
        public int     Amount;
        public int     PrimaryIdx;
        public int     SecondaryCount;
        public int     SpawnConfig;
        public fixed int SecondaryIdxs[MaxSecondary];

        public ObjectInstInfo(Vector3 pos, int prefab, int amount)
        {
            Position       = pos;
            PrefabID       = prefab;
            Amount         = amount;
            PrimaryIdx     = -1;
            SecondaryCount = 0;
            SpawnConfig    = NoSpawnConfig;
        }
    }
}
