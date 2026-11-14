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

    [Header("View Culling")]
    [SerializeField, Min(0f)] private float drawDistance = 0f;
    [SerializeField, Min(0f)] private float thinStartDistance = 160f;
    [SerializeField, Min(0.1f)] private float thinPower = 1.5f;
    [SerializeField, Range(0f, 1f)] private float thinWiden = 0.5f;
    [SerializeField, Min(0f)] private float cullMargin = 14f;
    [SerializeField, Min(0.1f)] private float replaceMoveStep = 2f;
    [SerializeField, Range(0.1f, 10f)] private float replaceTurnDegrees = 1f;
    [SerializeField, Min(0f)] private float shadowDistance = 90f;

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
    private static readonly int CullPlanesId = Shader.PropertyToID("cullPlanes");
    private static readonly int ViewOriginId = Shader.PropertyToID("viewOrigin");
    private static readonly int GrassFadeId = Shader.PropertyToID("grassFade");
    private static readonly int CullMarginId = Shader.PropertyToID("cullMargin");
    private static readonly int CullEnabledId = Shader.PropertyToID("cullEnabled");
    private static readonly int GrassViewId = Shader.PropertyToID("_GrassView");
    private const float unlimitedReach = 1e6f;

    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer climateBuffer;
    private ComputeBuffer nearHeightBuffer;
    private ComputeBuffer nearClimateBuffer;
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

    private Camera viewCamera;
    private readonly Plane[] frustumPlanes = new Plane[6];
    private readonly Vector4[] cullPlaneVectors = new Vector4[4];
    private Vector3 nearWindowMin;
    private Vector3 placedPosition;
    private Vector3 placedForward;
    private float placedFov;
    private float placedAspect;
    private bool windowReady;
    private bool placedWithCull;

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

        if (instanceBuffer != null)
        {
            Camera cam = ResolveCamera();
            PublishView(cam);

            if (windowReady && ViewChanged(cam))
                DispatchNear(cam);
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
        nearHeightBuffer?.Release();
        nearHeightBuffer = new ComputeBuffer(vertexCount, sizeof(float));
        nearClimateBuffer?.Release();
        nearClimateBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);

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

        UploadHeights(window, nearHeightBuffer, nearClimateBuffer);
        nearWindowMin = MapGenerator.WindowOriginWorld(center);
        renderBounds = WindowBounds(nearWindowMin);
        windowReady = true;
        DispatchNear(ResolveCamera());

        farMap = map;
        farCenter = center;
        hasFarCenter = true;
        sectorDirty = true;

        if (far == null) return;
        for (int i = 0; i < far.Length; i++)
            far[i].HasContent = false;
    }

    private void UploadHeights(in HeightWindow window, ComputeBuffer heights, ComputeBuffer climate)
    {
        new GatherHeightsJob
        {
            Heights = window,
            Resolution = terrainResolution,
            Output = heightMap,
            ClimateOutput = climateMap
        }.ScheduleParallel(terrainResolution, 16, default).Complete();

        heights.SetData(heightMap);
        climate.SetData(climateMap);
        BiomeShaderGlobals.ApplyTo(grassPlacementShader);
    }

    private Bounds WindowBounds(Vector3 boundsMin) => new Bounds(
        boundsMin + new Vector3(
            MapGenerator.generationSize * 0.5f,
            MapGenerator.maxTerrainHeight * 0.5f,
            MapGenerator.generationSize * 0.5f),
        boundsSize);

    private void PlaceBlock(in HeightWindow window, long blockCenter, ComputeBuffer target, int step, float widthScale, out Bounds bounds)
    {
        UploadHeights(window, heightMapBuffer, climateBuffer);

        Vector3 boundsMin = MapGenerator.WindowOriginWorld(blockCenter);
        bounds = WindowBounds(boundsMin);

        int cellStep = GrassLodBands.ValidStep(step, MapGenerator.generationSize);
        int gridWidth = GrassLodBands.CellsPerSide(MapGenerator.generationSize, cellStep);
        int instanceCount = GrassLodBands.Capacity(MapGenerator.generationSize, cellStep, bladesPerCell * densityFactor, placedCapacity);

        grassPlacementShader.SetInt(CullEnabledId, 0);
        DispatchPlace(boundsMin, target, heightMapBuffer, climateBuffer, cellStep, widthScale,
            Mathf.FloorToInt(boundsMin.x), Mathf.FloorToInt(boundsMin.z), gridWidth, gridWidth, instanceCount);
    }

    private void DispatchPlace(Vector3 boundsMin, ComputeBuffer target, ComputeBuffer heights, ComputeBuffer climate,
        int cellStep, float widthScale, int gridMinX, int gridMinZ, int gridWidth, int gridHeight, int instanceCount)
    {
        Vector3 boundsMax = boundsMin + new Vector3(MapGenerator.generationSize, 0f, MapGenerator.generationSize);

        grassPlacementShader.SetBuffer(kernelPlaceGrass, InstanceDataId, target);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, HeightMapId, heights);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, ClimateMapId, climate);

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
        if (threadGroups > 0)
            grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
    }

    private Camera ResolveCamera()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        return viewCamera;
    }

    private void PublishView(Camera cam)
    {
        float limit = isShadows && cam != null && shadowDistance > 0f
            ? (drawDistance > 0f ? math.min(shadowDistance, drawDistance) : shadowDistance)
            : 0f;
        Vector3 position = cam != null ? cam.transform.position : Vector3.zero;
        Shader.SetGlobalVector(GrassViewId, new Vector4(position.x, position.y, position.z, limit * limit));
    }

    private bool ViewChanged(Camera cam)
    {
        if (cam == null) return placedWithCull;
        if (!placedWithCull) return true;

        Transform camTransform = cam.transform;
        Vector3 delta = camTransform.position - placedPosition;
        if (delta.sqrMagnitude > replaceMoveStep * replaceMoveStep) return true;
        if (Vector3.Dot(camTransform.forward, placedForward) < math.cos(math.radians(replaceTurnDegrees))) return true;
        return cam.fieldOfView != placedFov || cam.aspect != placedAspect;
    }

    private void DispatchNear(Camera cam)
    {
        bool cull = cam != null;
        placedWithCull = cull;

        int gridMinX = Mathf.FloorToInt(nearWindowMin.x);
        int gridMinZ = Mathf.FloorToInt(nearWindowMin.z);
        int rectMinX = gridMinX;
        int rectMinZ = gridMinZ;
        int rectWidth = MapGenerator.generationSize;
        int rectHeight = MapGenerator.generationSize;
        bool any = true;

        grassPlacementShader.SetInt(CullEnabledId, cull ? 1 : 0);

        if (cull)
        {
            Transform camTransform = cam.transform;
            Vector3 position = camTransform.position;
            placedPosition = position;
            placedForward = camTransform.forward;
            placedFov = cam.fieldOfView;
            placedAspect = cam.aspect;

            GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            for (int i = 0; i < cullPlaneVectors.Length; i++)
            {
                Plane plane = frustumPlanes[i];
                Vector3 normal = plane.normal;
                cullPlaneVectors[i] = new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }

            bool limited = drawDistance > 0f;
            float reach = limited ? drawDistance : unlimitedReach;

            grassPlacementShader.SetVectorArray(CullPlanesId, cullPlaneVectors);
            grassPlacementShader.SetVector(ViewOriginId, position);
            grassPlacementShader.SetVector(GrassFadeId, new Vector4(limited ? thinStartDistance : unlimitedReach, reach, thinPower, thinWiden));
            grassPlacementShader.SetFloat(CullMarginId, cullMargin + CurvedWorld.MaxDrop(math.min(reach, MapGenerator.generationSize)));

            any = GrassCullMath.CellRect(
                new float2(position.x, position.z), reach,
                gridMinX, gridMinZ, MapGenerator.generationSize, MapGenerator.generationSize,
                out rectMinX, out rectMinZ, out rectWidth, out rectHeight);
        }

        int instanceCount = any
            ? (int)System.Math.Min((long)rectWidth * rectHeight * bladesPerCell * densityFactor, placedCapacity)
            : 0;

        DispatchPlace(nearWindowMin, instanceBuffer, nearHeightBuffer, nearClimateBuffer, 1, 1f,
            rectMinX, rectMinZ, rectWidth, rectHeight, instanceCount);
        ComputeBuffer.CopyCount(instanceBuffer, argsBuffer, sizeof(uint));
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
        nearHeightBuffer?.Release();
        nearClimateBuffer?.Release();
        argsBuffer?.Release();
        instanceBuffer = null;
        heightMapBuffer = null;
        climateBuffer = null;
        nearHeightBuffer = null;
        nearClimateBuffer = null;
        argsBuffer = null;
        nearBlock = null;
        windowReady = false;
        placedWithCull = false;
        Shader.SetGlobalVector(GrassViewId, Vector4.zero);

        ReleaseFar();

        if (heightMap.IsCreated) heightMap.Dispose();
        if (climateMap.IsCreated) climateMap.Dispose();
    }
}

public static class GrassCullMath
{
    public static bool CellRect(float2 center, float reach, int minX, int minZ, int width, int height,
        out int rectMinX, out int rectMinZ, out int rectWidth, out int rectHeight)
    {
        rectMinX = math.max(minX, (int)math.floor(center.x - reach));
        rectMinZ = math.max(minZ, (int)math.floor(center.y - reach));
        int maxX = math.min(minX + width, (int)math.ceil(center.x + reach));
        int maxZ = math.min(minZ + height, (int)math.ceil(center.y + reach));
        rectWidth = math.max(maxX - rectMinX, 0);
        rectHeight = math.max(maxZ - rectMinZ, 0);
        return rectWidth > 0 && rectHeight > 0;
    }
}
