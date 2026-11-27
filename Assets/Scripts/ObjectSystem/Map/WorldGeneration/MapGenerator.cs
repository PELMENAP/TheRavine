using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;
using TheRavine.Events;
using TheRavine.ViewControl;
using R3;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    using TheRavine.Base;
    using Unity.Mathematics;

    public partial class MapGenerator : MonoBehaviour, ISetAble, IWorldCommandTarget
    {
        [SerializeField] private ChunkGenerationSettings chunkGenerationSettings;
        [SerializeField] private ChunkGenerationSettings plateauSettings;
        private ChunkGenerationSettings activeSettings;

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
        private bool hasWindow, ready, disposed, nativeReleased;
        private ViewSector sector;
        private bool simPending;
        private int simShell, simStep;
        private int frame;
        private int lodRingCount;
        private bool evictionPending;
        private float nextEvictionScan;
        private long budgetTicks;
        private long worldTickReached;
        private readonly ErosionBudget erosionBudget = new();

        private IDisposable playerSubscription;
        private AEntity boundPlayer;

        public ChunkGenerationSettings Settings => activeSettings ?? chunkGenerationSettings;
        public RavineProfile Ravine { get; private set; }
        private static readonly int RavineProfileId = Shader.PropertyToID("_RavineProfile");
        public long WindowCenter => windowCenter;
        public ViewSector Sector => sector;
        public int LodRingCount => lodRingCount;
        public int LoadedChunkCount => mapData.Count;

        public long WorldTick
        {
            get
            {
                if (tickRules != null)
                {
                    long tick = (long)math.floor(SimulationClock.TimeD / tickRules.tickSeconds);
                    if (tick > worldTickReached) worldTickReached = tick;
                }
                return worldTickReached;
            }
        }

        private readonly ChunkData[] waterNeighborhood = new ChunkData[9];
        private int chunkSetVersion;
        private int streamedChunks;
        private int lookaheadDx, lookaheadDz;
        private int lookaheadVersion = -1;
        private long lookaheadWindow;
        public int UnloadStamp { get; private set; }
        public int EvictionTouchInterval => math.max(1, Settings.evictionIdleFrames / 2);
        private long[] chunkRequests = Array.Empty<long>();
        private int chunkRequestCount;

        public float WaterLevel { get; private set; } = float.MinValue;

        public CollisionTable Collision { get; private set; }

        public Transform terrainTransform, waterTransform;
        public MeshFilter terrainFilter;
        private int seed;
        public int Seed { get => seed; private set => seed = value; }
        private const int DefaultSeed = 16;

        private ObjectSystem objectSystem;
        private NalTickHandler nal;
        private RandomTickService randomTicks;
        private bool ownsTickRules;
        private bool ownsMapEventRules;
        private ChunkStore chunkStore;
        private IDisposable autosaveSubscription;
        private WorldRegistry boundRegistry;
        private string boundWorldId;

        [SerializeField] private Transform viewer;
        [SerializeField] private Vector3 viewerOffset;
        [SerializeField] private GrassSystem grassSystem;
        [SerializeField] private Texture2DArray biomeAlbedo;
        [SerializeField] private WorldTickRules tickRules;
        [SerializeField] private MapEventRules mapEventRules;
        [SerializeField] private FireViewSettings fireView = new();
        [SerializeField] private WildWindViewSettings wildWindView = new();
        [SerializeField] private WeatherViewSettings weatherView = new();
        [SerializeField, Min(0)] private int debugIgniteRadius = 6;
        [SerializeField, Min(1f)] private float debugWeatherRadius = 300f;
        [SerializeField, Min(0.1f)] private float nalApplyBudgetMs = 1f;
        [SerializeField, Min(1)] private int chunkRequestCapacity = 32;
        [SerializeField] private ObjectViewMotionSettings objectMotion = new();
        [SerializeField] private Shader ravineBackdropShader;
        [SerializeField] private Material ravineBackdropMaterial;

        public int ViewRingCount => math.min(lodRingCount, ObjectInfo.MaxVisibleRings);
        public int NalRingCount => tickRules != null ? math.min(ViewRingCount, tickRules.nalRings) : ViewRingCount;
        public int NalReach => NalRingCount * WindowSide + chunkScale;
        public RandomTickService RandomTicks => randomTicks;
        public GlobalMapEvents Events { get; private set; }

        public WorldCommandBuffer Commands { get; private set; }
        public WorldCommandStats CommandStats { get; } = new();
        public WorldCommandStats ImmediateStats { get; } = new();

        public ChunkGenerator chunkGenerator;
        private ChunkGenerator plateauGenerator;
        public Vector3 waterOffset;

        private IEndless[] endless;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Register(this);

            activeSettings = Settings.ShallowCopy();

            ServiceLocator.Services.TryGet(out WorldRegistry worldRegistry);
            bool worldLoaded = worldRegistry != null && worldRegistry.HasLoadedWorld;
            if (worldLoaded)
            {
                var config = worldRegistry.GetCurrentConfig();
                activeSettings.isRiver = config.generateRivers;
            }
            seed = worldLoaded ? worldRegistry.GetCurrentState().seed : DefaultSeed;
            mapData = new LongDictionary<ChunkData>(256);
            budgetTicks = FrameBudget.TicksFromMs(Settings.frameBudgetMs);

            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            Ravine = RavineProfile.From(Settings);
            chunkGenerator = new ChunkGenerator(Settings, Ravine, seed);
            plateauGenerator = Ravine.IsFlat ? null : new ChunkGenerator(PlateauSettings(), Ravine, seed);
            Shader.SetGlobalVector(RavineProfileId, new Vector4(Ravine.HalfWidth, Ravine.InvRun, Ravine.Height, 0f));
            if (worldLoaded) BindChunkStore(worldRegistry);

            sector = ViewOrientation.Sector;
            ViewOrientation.SectorChanged += OnSectorChanged;
            BiomeShaderGlobals.Apply(Settings, biomeAlbedo);

            BindFirstPlayer();

            if (chunkStore != null && ServiceLocator.Services.TryGet(out AutosaveCoordinator autosave))
                autosaveSubscription = autosave.SubscribeBeforeSave(() => SaveModifiedChunks(true));

            Commands = new WorldCommandBuffer(256);
            Collision?.Dispose();
            Collision = new CollisionTable(objectSystem != null ? objectSystem.infoRegistry : null, Settings.buildStep, Settings.levelHeight);
            chunkRequests = new long[chunkRequestCapacity];
            chunkRequestCount = 0;

            if (tickRules == null)
            {
                tickRules = ScriptableObject.CreateInstance<WorldTickRules>();
                ownsTickRules = true;
                RavineLog.Warning("[MapGenerator] WorldTickRules is not assigned, using defaults");
            }

            if (objectSystem != null)
            {
                randomTicks = new RandomTickService(this, objectSystem.infoRegistry, tickRules);
                if (Settings.endlessFlag[3])
                {
                    nal = new NalTickHandler(this, objectSystem.infoRegistry, randomTicks);
                    randomTicks.Register(nal);
                }

                if (mapEventRules == null)
                {
                    mapEventRules = ScriptableObject.CreateInstance<MapEventRules>();
                    ownsMapEventRules = true;
                    RavineLog.Warning("[MapGenerator] MapEventRules is not assigned, using defaults");
                }
                Events = new GlobalMapEvents(this, objectSystem.infoRegistry, randomTicks, mapEventRules, fireView, wildWindView, weatherView);
            }

            SetupEndless();
            ready = true;
            callback?.Invoke();
        }

        private void SetupEndless()
        {
            bool terrain = Settings.endlessFlag[0];
            int[] steps = Settings.lodSteps;
            lodRingCount = terrain ? math.min(Settings.maxLodRing, steps?.Length ?? 0) : 0;

            endless = new IEndless[5];
            if (terrain)
                endless[0] = new EndlessTerrain(this, Settings);
            if (Settings.endlessFlag[1])
                endless[1] = new EndlessLiquids(this, Settings);
            if (Settings.endlessFlag[2])
            {
                endless[2] = new EndlessObjects(this, objectSystem, objectMotion);
                if (lodRingCount > 0)
                    endless[3] = new FarObjectRenderer(this, objectSystem, Settings);
            }
            if (terrain && !Ravine.IsFlat)
                endless[4] = CreateBackdrop();
        }

        private RavineBackdrop CreateBackdrop()
        {
            if (ravineBackdropMaterial != null)
                return new RavineBackdrop(this, Settings, ravineBackdropMaterial, false);

            Shader shader = ravineBackdropShader != null ? ravineBackdropShader : Shader.Find(RavineBackdrop.ShaderName);
            if (shader == null)
            {
                RavineLog.Warning("[MapGenerator] Ravine backdrop shader is not assigned, distant walls are not drawn");
                return null;
            }

            var material = new Material(shader) { name = "RavineBackdrop" };
            Material terrainMaterial = terrainFilter != null && terrainFilter.TryGetComponent(out MeshRenderer terrainRenderer)
                ? terrainRenderer.sharedMaterial
                : null;
            if (terrainMaterial != null) material.CopyMatchingPropertiesFromMaterial(terrainMaterial);
            return new RavineBackdrop(this, Settings, material, true);
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
        }

        private void OnSectorChanged(ViewSector newSector)
        {
            if (newSector == sector) return;

            sector = newSector;
            if (endless != null)
            {
                for (int i = 0; i < endless.Length; i++)
                    if (endless[i] is IViewDirectional directional)
                        directional.SetSector(sector);
            }
            evictionPending = true;
        }

        public bool TryGetViewerChunk(out int x, out int z)
        {
            x = Position2Int.GetX(windowCenter);
            z = Position2Int.GetY(windowCenter);
            return hasWindow && viewer != null;
        }

        public bool ApplyImmediate(in WorldCommand command) =>
            WorldCommandBuffer.ApplyOne(this, in command, ImmediateStats, Commands);

        private void ApplyWorldCommands()
        {
            if (Commands == null || Commands.Count == 0) return;

            int pendingBefore = Commands.Count;
            long start = FrameBudget.Now;
            Commands.Apply(this, start + FrameBudget.TicksFromMs(nalApplyBudgetMs), CommandStats);
            int applyMs = (int)((FrameBudget.Now - start) * 1000 / System.Diagnostics.Stopwatch.Frequency);

            nal?.OnCommandsApplied(CommandStats, applyMs, pendingBefore, Commands.Count);

            ExtraUpdate();
        }

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
            randomTicks?.Update();
            Events?.Tick();
            nal?.Poll();
            FlushDirty();

            if (simPending)
                SupplySimSquare(FrameBudget.Now + (deadline - FrameBudget.Now) / 2);

            SupplyLookahead(FrameBudget.Now + (deadline - FrameBudget.Now) / 2);

            for (int i = 0; i < endless.Length; i++)
                endless[i]?.Tick(deadline);

            ProcessChunkRequests(deadline);

            if (evictionPending || Time.unscaledTime >= nextEvictionScan)
                EvictChunks();
        }

        public void ExtraUpdate() => FlushDirty();

        private ChunkGenerationSettings PlateauSettings()
        {
            ChunkGenerationSettings source = plateauSettings;
            if (source == null || source.regions == null || source.regions.Length == 0)
            {
                RavineLog.Warning("[MapGenerator] Plateau settings are not set, the plateau uses the valley settings");
                source = Settings;
            }

            ChunkGenerationSettings plateau = source.ShallowCopy();
            plateau.endlessFlag = Settings.endlessFlag;
            plateau.isRiver = Settings.isRiver;
            return plateau;
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

            ViewOrientation.SectorChanged -= OnSectorChanged;

            playerSubscription?.Dispose();
            playerSubscription = null;
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
        }

        [ContextMenu("Ignite Fire Near Viewer")]
        private void IgniteNearViewer()
        {
            FireEvent fire = Events?.Fire;
            if (fire == null || viewer == null) return;

            Vector3 p = viewer.position;
            long centre = FireEvent.CellOf(p.x, p.z);
            int cx = Position2Int.GetX(centre);
            int cz = Position2Int.GetY(centre);
            int lit = 0;
            for (int dz = -debugIgniteRadius; dz <= debugIgniteRadius && lit < 5; dz++)
            for (int dx = -debugIgniteRadius; dx <= debugIgniteRadius && lit < 5; dx++)
                if (fire.TryIgnite(Position2Int.Pack(cx + dx, cz + dz))) lit++;
            RavineLog.Warning($"[MapGenerator] Ignited {lit} objects near the viewer");
        }

        [ContextMenu("Spawn Rain Near Viewer")]
        private void SpawnRainNearViewer() => SpawnWeatherNearViewer(0.8f, 0f, "rain");

        [ContextMenu("Spawn Thunderstorm Near Viewer")]
        private void SpawnThunderstormNearViewer() => SpawnWeatherNearViewer(0.9f, 0.9f, "thunderstorm");

        [ContextMenu("Spawn Dry Thunderstorm Near Viewer")]
        private void SpawnDryThunderstormNearViewer() => SpawnWeatherNearViewer(0.1f, 0.9f, "dry thunderstorm");

        private void SpawnWeatherNearViewer(float moisture, float charge, string label)
        {
            WeatherEvent weather = Events?.Weather;
            if (weather == null || viewer == null || !WorldAuthority.IsAuthority) return;

            Vector3 p = viewer.position;
            bool spawned = weather.Spawn(new float2(p.x, p.z), moisture, charge, debugWeatherRadius, weather.Rules.lifeMax);
            RavineLog.Warning($"[MapGenerator] Spawn {label} near the viewer: {(spawned ? "ok" : "no free cell")}");
        }

        private void OnDestroy()
        {
            OnDisable();
            if (nativeReleased) return;
            nativeReleased = true;

            UnloadStamp++;
            foreach (var cd in mapData) cd.Value.Dispose();
            mapData.Clear();

            while (chunkPool.Count > 0)
                chunkPool.Pop().Dispose();

            sampleChunk = null;
            sampleKey = long.MinValue;

            Events?.Dispose();
            Events = null;
            if (ownsMapEventRules && mapEventRules != null) Destroy(mapEventRules);
            ownsMapEventRules = false;
            nal?.Dispose();
            nal = null;
            randomTicks?.Dispose();
            randomTicks = null;
            if (ownsTickRules && tickRules != null) Destroy(tickRules);
            ownsTickRules = false;
            Commands?.Dispose();
            Commands = null;
            Collision?.Dispose();
            Collision = null;
            chunkGenerator?.Dispose();
            plateauGenerator?.Dispose();
            plateauGenerator = null;
        }
    }
}
