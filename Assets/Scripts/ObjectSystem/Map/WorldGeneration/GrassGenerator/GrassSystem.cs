using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.ViewControl;

public class GrassSystem : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private ComputeShader grassPlacementShader;
    [SerializeField] private ComputeShader grassLodShader;
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
    [SerializeField] private Vector3 lodEndDistances = new(60f, 120f, 200f);
    [SerializeField] private Vector3 lodKeepFractions = new(1f, 0.5f, 0.25f);
    [SerializeField] private float lodFadeWidth = 15f;
    [SerializeField] private float lodRefreshDistance = 4f;

    private const int terrainResolution = 1 + 3 * MapGenerator.mapChunkSize;
    private const int vertexCount = terrainResolution * terrainResolution;
    private const int maxGrassInstances = 300000;
    private const int instanceStride = 28;
    private const int argsStride = 5;
    private const int argsStrideBytes = argsStride * sizeof(uint);
    private const int partitionThreads = 64;
    private const int partitionGroupsPerRow = 4096;

    private static readonly int InstanceDataId = Shader.PropertyToID("instanceData");
    private static readonly int PlacedId = Shader.PropertyToID("placed");
    private static readonly int PlacedCountId = Shader.PropertyToID("placedCount");
    private static readonly int LodPivotId = Shader.PropertyToID("lodPivot");
    private static readonly int LodEndsId = Shader.PropertyToID("lodEnds");
    private static readonly int LodKeepId = Shader.PropertyToID("lodKeep");
    private static readonly int RowStrideId = Shader.PropertyToID("rowStride");
    private static readonly int[] BandIds =
    {
        Shader.PropertyToID("band0"),
        Shader.PropertyToID("band1"),
        Shader.PropertyToID("band2")
    };
    
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer climateBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer countBuffer;
    private readonly ComputeBuffer[] bandBuffers = new ComputeBuffer[GrassLodBands.BandCount];
    private readonly MaterialPropertyBlock[] bandBlocks = new MaterialPropertyBlock[GrassLodBands.BandCount];
    private readonly Mesh[] bandMeshes = new Mesh[GrassLodBands.BandCount];
    
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private readonly uint[] args = new uint[argsStride * GrassLodBands.BandCount];
    private int kernelPartition;
    private int activeBands;
    private int placedCapacity;
    private int partitionGroupsX;
    private int partitionGroupsY;
    private bool lodActive;
    private bool hasPlaced;
    private float refreshSqr;
    private Vector2 windowCenter;
    private Vector2 lastPartitionPivot;
    
    private int instanceCount;

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
        else if (lodActive && hasPlaced && PivotTravelSqr() > refreshSqr)
        {
            Partition();
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
        argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);

        long windowCells = (long)MapGenerator.generationSize * MapGenerator.generationSize;
        placedCapacity = (int)System.Math.Min(windowCells * bladesPerCell * densityFactor, (long)maxGrassInstances * densityFactor);

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

        float terrainWidth = MapGenerator.generationSize;
        float terrainHeight = MapGenerator.generationSize;

        grassPlacementShader.SetFloat("terrainInvWidth",  1f / terrainWidth);
        grassPlacementShader.SetFloat("terrainInvHeight", 1f / terrainHeight);

        InitializeLod();

        if (!heightMap.IsCreated)
            heightMap = new NativeArray<float>(vertexCount, Allocator.Persistent);

        if (!climateMap.IsCreated)
            climateMap = new NativeArray<float2>(vertexCount, Allocator.Persistent);

        heightMapBuffer?.Release();
        heightMapBuffer = new ComputeBuffer(vertexCount, sizeof(float));
        climateBuffer?.Release();
        climateBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
    }
    
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

        Vector3 boundsMin = MapGenerator.WindowOriginWorld(center);

        Vector3 centerBound = boundsMin + new Vector3(
            MapGenerator.generationSize * 0.5f,
            MapGenerator.maxTerrainHeight * 0.5f,
            MapGenerator.generationSize * 0.5f);
        renderBounds = new Bounds(centerBound, boundsSize);

        Vector3 boundsMax = boundsMin + new Vector3(MapGenerator.generationSize, 0f, MapGenerator.generationSize);

        grassPlacementShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "heightMap", heightMapBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "climateMap", climateBuffer);

        int gridWidth = MapGenerator.generationSize;
        int gridHeight = MapGenerator.generationSize;
        int gridMinX = Mathf.FloorToInt(boundsMin.x);
        int gridMinZ = Mathf.FloorToInt(boundsMin.z);

        instanceCount = placedCapacity;
        windowCenter = new Vector2(boundsMin.x + gridWidth * 0.5f, boundsMin.z + gridHeight * 0.5f);

        grassPlacementShader.SetInt("instanceCount", instanceCount);
        grassPlacementShader.SetInt("gridWidth", gridWidth);
        grassPlacementShader.SetInt("gridHeight", gridHeight);
        grassPlacementShader.SetInt("gridMinX", gridMinX);
        grassPlacementShader.SetInt("gridMinZ", gridMinZ);

        grassPlacementShader.SetVector("worldBoundsMin", boundsMin);
        grassPlacementShader.SetVector("worldBoundsMax", boundsMax);

        int threadGroups = Mathf.CeilToInt(instanceCount / 64f);

        instanceBuffer.SetCounterValue(0);
        grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);

        hasPlaced = true;

        if (lodActive) Partition();
        else ComputeBuffer.CopyCount(instanceBuffer, argsBuffer, sizeof(uint));
    }

    private void InitializeLod()
    {
        lodActive = grassLodShader != null;
        activeBands = lodActive ? GrassLodBands.BandCount : 1;
        float refresh = lodRefreshDistance > 0f ? lodRefreshDistance : 0f;
        refreshSqr = refresh * refresh;

        for (int band = 0; band < activeBands; band++)
        {
            Mesh mesh = lodMeshes != null && band < lodMeshes.Length && lodMeshes[band] != null ? lodMeshes[band] : grassMesh;
            bandMeshes[band] = mesh;

            int offset = band * argsStride;
            args[offset] = mesh.GetIndexCount(0);
            args[offset + 1] = 0;
            args[offset + 2] = mesh.GetIndexStart(0);
            args[offset + 3] = mesh.GetBaseVertex(0);
            args[offset + 4] = 0;

            bandBlocks[band] = new MaterialPropertyBlock();
        }

        argsBuffer.SetData(args);

        if (!lodActive)
        {
            RavineLog.Warning("Grass LOD shader not assigned. Grass is drawn without LOD");
            bandBlocks[0].SetBuffer(InstanceDataId, instanceBuffer);
            return;
        }

        kernelPartition = grassLodShader.FindKernel("PartitionGrass");
        countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

        Vector3 ends = lodEndDistances;
        for (int band = 0; band < activeBands; band++)
        {
            int capacity = GrassLodBands.Capacity(band, ends, MapGenerator.generationSize, bladesPerCell * densityFactor, placedCapacity);
            bandBuffers[band] = new ComputeBuffer(System.Math.Max(capacity, 1), instanceStride, ComputeBufferType.Append);
            bandBlocks[band].SetBuffer(InstanceDataId, bandBuffers[band]);
        }

        int groups = Mathf.Max(1, Mathf.CeilToInt(placedCapacity / (float)partitionThreads));
        partitionGroupsX = Mathf.Min(groups, partitionGroupsPerRow);
        partitionGroupsY = Mathf.CeilToInt(groups / (float)partitionGroupsX);

        grassLodShader.SetVector(LodEndsId, GrassLodBands.PackEnds(ends, lodFadeWidth));
        grassLodShader.SetVector(LodKeepId, GrassLodBands.PackKeep(lodKeepFractions));
        grassLodShader.SetInt(RowStrideId, partitionGroupsX * partitionThreads);
    }

    private Vector2 CurrentPivot()
    {
        if (!ViewOrientation.HasPivot) return windowCenter;

        Vector3 pivot = ViewOrientation.Pivot;
        return new Vector2(pivot.x, pivot.z);
    }

    private float PivotTravelSqr()
    {
        Vector2 delta = CurrentPivot() - lastPartitionPivot;
        return delta.sqrMagnitude;
    }

    private void Partition()
    {
        lastPartitionPivot = CurrentPivot();

        ComputeBuffer.CopyCount(instanceBuffer, countBuffer, 0);

        grassLodShader.SetVector(LodPivotId, new Vector4(lastPartitionPivot.x, lastPartitionPivot.y, 0f, 0f));
        grassLodShader.SetBuffer(kernelPartition, PlacedId, instanceBuffer);
        grassLodShader.SetBuffer(kernelPartition, PlacedCountId, countBuffer);

        for (int band = 0; band < activeBands; band++)
        {
            bandBuffers[band].SetCounterValue(0);
            grassLodShader.SetBuffer(kernelPartition, BandIds[band], bandBuffers[band]);
        }

        grassLodShader.Dispatch(kernelPartition, partitionGroupsX, partitionGroupsY, 1);

        for (int band = 0; band < activeBands; band++)
            ComputeBuffer.CopyCount(bandBuffers[band], argsBuffer, band * argsStrideBytes + sizeof(uint));
    }

    private void RenderGrass()
    {
        if (instanceBuffer == null) return;

        for (int band = 0; band < activeBands; band++)
        {
            Graphics.DrawMeshInstancedIndirect(
                bandMeshes[band],
                0,
                grassMaterial,
                renderBounds,
                argsBuffer,
                band * argsStrideBytes,
                bandBlocks[band],
                band == 0 && isShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }
    }

    private void OnDisable()
    {
        GrassSoilBuffer.Release();
        instanceBuffer?.Release();
        heightMapBuffer?.Release();
        climateBuffer?.Release();
        argsBuffer?.Release();
        countBuffer?.Release();
        instanceBuffer = null;
        heightMapBuffer = null;
        climateBuffer = null;
        argsBuffer = null;
        countBuffer = null;

        for (int band = 0; band < bandBuffers.Length; band++)
        {
            bandBuffers[band]?.Release();
            bandBuffers[band] = null;
            bandBlocks[band] = null;
            bandMeshes[band] = null;
        }

        lodActive = false;
        hasPlaced = false;
        activeBands = 0;

        if (heightMap.IsCreated) heightMap.Dispose();
        if (climateMap.IsCreated) climateMap.Dispose();
    }
}