using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.ViewControl;

public partial class GrassSystem : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private ComputeShader grassPlacementShader;
    [Header("Placement Parameters")]
    [SerializeField] private float grassDensity = 0.3f;
    [SerializeField] private int bladesPerCell = 2;
    [SerializeField] private float scaleMin = 0.8f;
    [SerializeField] private float scaleMax = 1.2f;
    [SerializeField] private float rotationVariation = 360f;

    [SerializeField] private float globalMinHeight = 5f;
    [SerializeField] private float globalMaxHeight = 9f;


    [Header("Scale Variation")]
    [SerializeField] private float scaleNoiseScale = 0.05f;
    [SerializeField] private float scaleNoiseInfluence = 0.5f;
    [SerializeField] private int scaleOctaves = 2;
    [SerializeField] private float scalePersistence = 0.5f;
    [SerializeField] private float scaleLacunarity = 2.0f;

    [Header("Density Control")]
    [SerializeField] private float noiseScale = 0.1f;
    [SerializeField] private float densityMinThreshold = 0.2f;
    [SerializeField] private float densityMaxThreshold = 1.0f;
    [SerializeField] private int octaves = 3;
    [SerializeField] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2.0f;

    [Header("Climate Mask")]
    [SerializeField] private Vector4 temperatureRange = new(-1f, 0f, 1f, 2f);
    [SerializeField] private Vector4 moistureRange = new(-1f, 0f, 1f, 2f);

    [Header("LOD")]
    [SerializeField] private Mesh[] lodMeshes = new Mesh[GrassLodBands.BandCount];
    [SerializeField] private Vector2Int lodCellSteps = new(4, 8);
    [SerializeField] private Vector2 lodWidthScales = new(2f, 3f);
    [SerializeField, Min(1)] private int lodBlocksPerFrame = 1;

    private const int terrainResolution = 1 + 3 * MapGenerator.mapChunkSize;
    private const int vertexCount = terrainResolution * terrainResolution;
    private const int maxGrassInstances = 300000;
    private const int instanceStride = 28;
    private const int argsStride = 5;
    private const int argsStrideBytes = argsStride * sizeof(uint);
    private const int placeThreads = 64;

    private static readonly int InstanceDataId = Shader.PropertyToID("instanceData");
    private static readonly int HeightMapId = Shader.PropertyToID("heightMap");
    private static readonly int ClimateMapId = Shader.PropertyToID("climateMap");
    private static readonly int InstanceCountId = Shader.PropertyToID("instanceCount");
    private static readonly int GridWidthId = Shader.PropertyToID("gridWidth");
    private static readonly int GridHeightId = Shader.PropertyToID("gridHeight");
    private static readonly int GridMinXId = Shader.PropertyToID("gridMinX");
    private static readonly int GridMinZId = Shader.PropertyToID("gridMinZ");
    private static readonly int WorldBoundsMinId = Shader.PropertyToID("worldBoundsMin");
    private static readonly int WorldBoundsMaxId = Shader.PropertyToID("worldBoundsMax");
    private static readonly int CellStepId = Shader.PropertyToID("cellStep");
    private static readonly int WidthScaleId = Shader.PropertyToID("widthScale");

    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer climateBuffer;
    private ComputeBuffer argsBuffer;
    private MaterialPropertyBlock nearBlock;
    private readonly Mesh[] ringMeshes = new Mesh[GrassLodBands.BandCount];

    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private readonly uint[] args = new uint[argsStride];
    private int placedCapacity;

    private int densityFactor;
    private bool isGrass, isShadows;
    private NativeArray<float> heightMap;
    private NativeArray<float2> climateMap;

    private MapGenerator pendingMap;
    private long pendingCenter;
    private bool hasPending;
    private Vector3 boundsSize;

    private void Start()
    {
        try
        {
            var gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            densityFactor = gameSettings.grassDensityFactor;
            isGrass = gameSettings.enableGrass;
            isShadows = gameSettings.enableGrassShadows;
        }
        catch (System.Exception)
        {
            RavineLog.Warning("GlobalSettingsController not found. Create a default grass");
            densityFactor = 5;
            isGrass = true;
            isShadows = true;
        }

        if(!isGrass) return;
        InitializeSystem();
    }

    private void Update()
    {
        if (!isGrass) return;

        if (hasPending && instanceBuffer != null)
        {
            hasPending = false;
            PlaceGrass(pendingMap, pendingCenter);
        }

        if (hasFarCenter && far != null)
        {
            if (sectorDirty) ApplySector();
            BuildFar();
        }

        RenderGrass();
    }

    private void InitializeSystem()
    {
        GrassSoilBuffer.EnsureBound();

        if (grassPlacementShader == null)
        {
            RavineLog.Error("Grass placement shader not assigned!");
            return;
        }

        kernelPlaceGrass = grassPlacementShader.FindKernel("PlaceGrass");

        instanceBuffer = new ComputeBuffer(maxGrassInstances * densityFactor, instanceStride, ComputeBufferType.Append);
        argsBuffer = new ComputeBuffer(argsStride, sizeof(uint), ComputeBufferType.IndirectArguments);

        long windowCells = (long)MapGenerator.generationSize * MapGenerator.generationSize;
        placedCapacity = (int)System.Math.Min(windowCells * bladesPerCell * densityFactor, (long)maxGrassInstances * densityFactor);

        for (int ring = 0; ring < ringMeshes.Length; ring++)
            ringMeshes[ring] = MeshForRing(ring);

        Mesh nearMesh = ringMeshes[0];
        args[0] = nearMesh.GetIndexCount(0);
        args[1] = 0;
        args[2] = nearMesh.GetIndexStart(0);
        args[3] = nearMesh.GetBaseVertex(0);
        args[4] = 0;

        nearBlock = new MaterialPropertyBlock();
        nearBlock.SetBuffer(InstanceDataId, instanceBuffer);

        float side = MapGenerator.generationSize;
        boundsSize = new Vector3(side, MapGenerator.maxTerrainHeight * 2f, side);
        renderBounds = new Bounds(Vector3.zero, boundsSize);

        grassPlacementShader.SetFloat("density", grassDensity);
        grassPlacementShader.SetInt("bladesPerCell", bladesPerCell * densityFactor);
        grassPlacementShader.SetFloat("scaleMin", scaleMin);
        grassPlacementShader.SetFloat("scaleMax", scaleMax);
        grassPlacementShader.SetFloat("rotationVariation", rotationVariation);
        grassPlacementShader.SetFloat("noiseScale", noiseScale);
        grassPlacementShader.SetFloat("densityMinThreshold", densityMinThreshold);
        grassPlacementShader.SetFloat("densityMaxThreshold", densityMaxThreshold);
        grassPlacementShader.SetInt("octaves", octaves);
        grassPlacementShader.SetFloat("persistence", persistence);
        grassPlacementShader.SetFloat("lacunarity", lacunarity);
        grassPlacementShader.SetInt("terrainResolution", terrainResolution);

        grassPlacementShader.SetFloat("scaleNoiseScale", scaleNoiseScale);
        grassPlacementShader.SetFloat("scaleNoiseInfluence", scaleNoiseInfluence);
        grassPlacementShader.SetInt("scaleOctaves", scaleOctaves);
        grassPlacementShader.SetFloat("scalePersistence", scalePersistence);
        grassPlacementShader.SetFloat("scaleLacunarity", scaleLacunarity);

        grassPlacementShader.SetVector("climateTemperatureRange", temperatureRange);
        grassPlacementShader.SetVector("climateMoistureRange", moistureRange);

        grassPlacementShader.SetFloat("globalMinHeight", globalMinHeight);
        grassPlacementShader.SetFloat("globalMaxHeight", globalMaxHeight);

        argsBuffer.SetData(args);

        float terrainWidth = MapGenerator.generationSize;
        float terrainHeight = MapGenerator.generationSize;

        grassPlacementShader.SetFloat("terrainInvWidth",  1f / terrainWidth);
        grassPlacementShader.SetFloat("terrainInvHeight", 1f / terrainHeight);

        if (!heightMap.IsCreated)
            heightMap = new NativeArray<float>(vertexCount, Allocator.Persistent);

        if (!climateMap.IsCreated)
            climateMap = new NativeArray<float2>(vertexCount, Allocator.Persistent);

        heightMapBuffer?.Release();
        heightMapBuffer = new ComputeBuffer(vertexCount, sizeof(float));
        climateBuffer?.Release();
        climateBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);

        ViewOrientation.SectorChanged += OnSectorChanged;
    }

    private Mesh MeshForRing(int ring) =>
        lodMeshes != null && ring < lodMeshes.Length && lodMeshes[ring] != null ? lodMeshes[ring] : grassMesh;

    public void UpdateGrassPlacement(MapGenerator map, long center)
    {
        pendingMap = map;
        pendingCenter = center;
        hasPending = true;

        if (ServiceLocator.Services.TryGet(out GrassTrampleField trampleField) && !trampleField.IsDisposed)
            trampleField.Recenter(center);

        if (!isGrass || instanceBuffer == null) return;

        hasPending = false;
        PlaceGrass(map, center);
    }

    private void PlaceGrass(MapGenerator map, long center)
    {
        if (map == null || !map.TryBuildHeightWindow(center, out HeightWindow window)) return;

        if (!farPoolReady) BuildFarPool(map);

        PlaceBlock(window, center, instanceBuffer, 1, 1f, out renderBounds);
        ComputeBuffer.CopyCount(instanceBuffer, argsBuffer, sizeof(uint));

        farMap = map;
        farCenter = center;
        hasFarCenter = true;
        sectorDirty = true;

        if (far == null) return;
        for (int i = 0; i < far.Length; i++)
            far[i].HasContent = false;
    }

    private void PlaceBlock(in HeightWindow window, long blockCenter, ComputeBuffer target, int step, float widthScale, out Bounds bounds)
    {
        new GatherHeightsJob
        {
            Heights = window,
            Resolution = terrainResolution,
            Output = heightMap,
            ClimateOutput = climateMap
        }.ScheduleParallel(terrainResolution, 16, default).Complete();

        heightMapBuffer.SetData(heightMap);
        climateBuffer.SetData(climateMap);
        BiomeShaderGlobals.ApplyTo(grassPlacementShader);

        Vector3 boundsMin = MapGenerator.WindowOriginWorld(blockCenter);

        Vector3 centerBound = boundsMin + new Vector3(
            MapGenerator.generationSize * 0.5f,
            MapGenerator.maxTerrainHeight * 0.5f,
            MapGenerator.generationSize * 0.5f);
        bounds = new Bounds(centerBound, boundsSize);

        Vector3 boundsMax = boundsMin + new Vector3(MapGenerator.generationSize, 0f, MapGenerator.generationSize);

        grassPlacementShader.SetBuffer(kernelPlaceGrass, InstanceDataId, target);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, HeightMapId, heightMapBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, ClimateMapId, climateBuffer);

        int cellStep = GrassLodBands.ValidStep(step, MapGenerator.generationSize);
        int gridWidth = GrassLodBands.CellsPerSide(MapGenerator.generationSize, cellStep);
        int gridHeight = gridWidth;
        int gridMinX = Mathf.FloorToInt(boundsMin.x);
        int gridMinZ = Mathf.FloorToInt(boundsMin.z);

        int instanceCount = GrassLodBands.Capacity(MapGenerator.generationSize, cellStep, bladesPerCell * densityFactor, placedCapacity);

        grassPlacementShader.SetInt(InstanceCountId, instanceCount);
        grassPlacementShader.SetInt(GridWidthId, gridWidth);
        grassPlacementShader.SetInt(GridHeightId, gridHeight);
        grassPlacementShader.SetInt(GridMinXId, gridMinX);
        grassPlacementShader.SetInt(GridMinZId, gridMinZ);
        grassPlacementShader.SetInt(CellStepId, cellStep);
        grassPlacementShader.SetFloat(WidthScaleId, widthScale);

        grassPlacementShader.SetVector(WorldBoundsMinId, boundsMin);
        grassPlacementShader.SetVector(WorldBoundsMaxId, boundsMax);

        int threadGroups = Mathf.CeilToInt(instanceCount / (float)placeThreads);

        target.SetCounterValue(0);
        grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
    }

    private void RenderGrass()
    {
        if (instanceBuffer == null) return;

        Graphics.DrawMeshInstancedIndirect(
            ringMeshes[0],
            0,
            grassMaterial,
            renderBounds,
            argsBuffer,
            0,
            nearBlock,
            isShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            false,
            gameObject.layer
        );

        RenderFar();
    }
    
    private void OnDisable()
    {
        ViewOrientation.SectorChanged -= OnSectorChanged;

        GrassSoilBuffer.Release();
        instanceBuffer?.Release();
        heightMapBuffer?.Release();
        climateBuffer?.Release();
        argsBuffer?.Release();
        instanceBuffer = null;
        heightMapBuffer = null;
        climateBuffer = null;
        argsBuffer = null;
        nearBlock = null;

        ReleaseFar();

        if (heightMap.IsCreated) heightMap.Dispose();
        if (climateMap.IsCreated) climateMap.Dispose();
    }
}
