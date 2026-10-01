using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using TheRavine.Base;
using TheRavine.Generator;

public class GrassSystem : MonoBehaviour
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
    
    private const int terrainResolution = 1 + 3 * MapGenerator.mapChunkSize;
    private const int vertexCount = terrainResolution * terrainResolution;
    private const int maxGrassInstances = 300000;
    
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer climateBuffer;
    private ComputeBuffer argsBuffer;
    
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private readonly uint[] args = new uint[5];
    
    private int instanceCount;

    private int densityFactor;
    private bool isGrass, isShadows;
    private NativeArray<float> heightMap;
    private NativeArray<float2> climateMap;

    private MapGenerator pendingMap;
    private long pendingCenter;
    private bool hasPending;

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

        RenderGrass();
    }
    
    private void InitializeSystem()
    {
        
        if (grassPlacementShader == null)
        {
            RavineLog.Error("Grass placement shader not assigned!");
            return;
        }
        
        kernelPlaceGrass = grassPlacementShader.FindKernel("PlaceGrass");
        
        instanceBuffer = new ComputeBuffer(maxGrassInstances * densityFactor, 28, ComputeBufferType.Append);
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = 0;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        args[4] = 0;
        
        renderBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        
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

        int gridWidth = MapGenerator.generationSize;
        int gridHeight = MapGenerator.generationSize;
        int totalGridPoints = gridWidth * gridHeight * bladesPerCell * densityFactor;
        instanceCount = Mathf.Min(totalGridPoints, maxGrassInstances * densityFactor);

        args[1] = (uint)instanceCount;
        argsBuffer.SetData(args);
        
        float terrainWidth = MapGenerator.generationSize;
        float terrainHeight = MapGenerator.generationSize;
        
        grassPlacementShader.SetInt("instanceCount", instanceCount);

        grassPlacementShader.SetInt("gridWidth", gridWidth);
        grassPlacementShader.SetInt("gridHeight", gridHeight);

        grassPlacementShader.SetFloat("terrainInvWidth",  1f / terrainWidth);
        grassPlacementShader.SetFloat("terrainInvHeight", 1f / terrainHeight);

        grassMaterial.SetBuffer("instanceData", instanceBuffer);

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
        Vector3 boundsMax = boundsMin + new Vector3(MapGenerator.generationSize, 0f, MapGenerator.generationSize);

        grassPlacementShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "heightMap", heightMapBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "climateMap", climateBuffer);

        grassPlacementShader.SetInt("gridMinX", Mathf.FloorToInt(boundsMin.x));
        grassPlacementShader.SetInt("gridMinZ", Mathf.FloorToInt(boundsMin.z));

        grassPlacementShader.SetVector("worldBoundsMin", boundsMin);
        grassPlacementShader.SetVector("worldBoundsMax", boundsMax);

        int threadGroups = Mathf.CeilToInt(instanceCount / 64f);

        instanceBuffer.SetCounterValue(0);
        grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);

        ComputeBuffer.CopyCount(instanceBuffer, argsBuffer, sizeof(uint));
    }
    
    
    private void RenderGrass()
    {
        if (instanceBuffer == null) return;

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            renderBounds,
            argsBuffer,
            0,
            null,
            isShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }
    
    private void OnDisable()
    {
        instanceBuffer?.Release();
        heightMapBuffer?.Release();
        climateBuffer?.Release();
        argsBuffer?.Release();
        instanceBuffer = null;
        heightMapBuffer = null;
        climateBuffer = null;
        argsBuffer = null;

        if (heightMap.IsCreated) heightMap.Dispose();
        if (climateMap.IsCreated) climateMap.Dispose();
    }
}