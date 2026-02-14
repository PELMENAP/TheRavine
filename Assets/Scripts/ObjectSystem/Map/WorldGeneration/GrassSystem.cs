using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.Generator;

public class GrassSystem : MonoBehaviour
{
    [Header("Target Mesh")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private Transform targetTransform;
    
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
    
    private const int terrainResolution = 1 + 3 * MapGenerator.mapChunkSize;
    private const int vertexCount = terrainResolution * terrainResolution;
    private const int maxGrassInstances = 200000;
    
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer argsBuffer;
    
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private readonly uint[] args = new uint[5];
    
    private int lastInstanceCount;
    private Vector3 lastPosition;

    private GlobalSettings gameSettings;
    
    void Start()
    {
        gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();

        if(gameSettings.enableGrass) return;
        InitializeSystem();
    }
    
    void Update()
    {
        if (gameSettings.enableGrass) return;

        if(targetTransform.position != lastPosition)
        {
            UpdateGrassPlacement();
            lastPosition = targetTransform.position;
        }
        
        RenderGrass();
    }
    
    private void InitializeSystem()
    {
        if (grassPlacementShader == null)
        {
            Debug.LogError("Grass placement shader not assigned!");
            return;
        }
        
        kernelPlaceGrass = grassPlacementShader.FindKernel("PlaceGrass");
        
        instanceBuffer = new ComputeBuffer(maxGrassInstances * gameSettings.grassDensityFactor, 80);
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = 0;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        args[4] = 0;
        
        renderBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        
        grassPlacementShader.SetFloat("density", grassDensity);
        grassPlacementShader.SetInt("bladesPerCell", bladesPerCell * gameSettings.grassDensityFactor);
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
    }
    
    void UpdateGrassPlacement()
    {
        Mesh mesh = targetMeshFilter.sharedMesh;
        if (mesh == null) return;
        
        var vertices = mesh.vertices;
        var meshBounds = mesh.bounds;
        Matrix4x4 localToWorld = targetTransform.localToWorldMatrix;
        
        if (heightMapBuffer == null || heightMapBuffer.count != vertexCount)
        {
            heightMapBuffer?.Release();
            heightMapBuffer = new ComputeBuffer(vertexCount, sizeof(float));
        }
        
        NativeArray<float> heightMap = new NativeArray<float>(vertexCount, Allocator.Temp);
        
        for (int k = 0; k < vertexCount; k++)
        {
            int i = k / terrainResolution;
            int j = k % terrainResolution;
            int vertexIndex = (terrainResolution - j - 1) * terrainResolution + (terrainResolution - i - 1);
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[vertexIndex]);
            heightMap[vertexCount - 1 - k] = worldPos.y;
        }
        
        heightMapBuffer.SetData(heightMap);
        heightMap.Dispose();
        
        Bounds worldBounds = GeneratorExtensions.TransformBounds(meshBounds, localToWorld);
        
        int minX = Mathf.FloorToInt(worldBounds.min.x);
        int maxX = Mathf.CeilToInt(worldBounds.max.x);
        int minZ = Mathf.FloorToInt(worldBounds.min.z);
        int maxZ = Mathf.CeilToInt(worldBounds.max.z);
        
        int gridWidth = maxX - minX;
        int gridHeight = maxZ - minZ;
        int totalGridPoints = gridWidth * gridHeight * bladesPerCell * gameSettings.grassDensityFactor;
        int instanceCount = Mathf.Min(totalGridPoints, maxGrassInstances * gameSettings.grassDensityFactor);
        
        float terrainWidth = worldBounds.size.x;
        float terrainHeight = worldBounds.size.z;
        
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "heightMap", heightMapBuffer);
        grassPlacementShader.SetInt("instanceCount", instanceCount);
        grassPlacementShader.SetInt("gridMinX", minX);
        grassPlacementShader.SetInt("gridMinZ", minZ);
        grassPlacementShader.SetInt("gridWidth", gridWidth);
        grassPlacementShader.SetInt("gridHeight", gridHeight);
        grassPlacementShader.SetVector("worldBoundsMin", worldBounds.min);
        grassPlacementShader.SetVector("worldBoundsMax", worldBounds.max);
        grassPlacementShader.SetFloat("terrainWidth", terrainWidth);
        grassPlacementShader.SetFloat("terrainHeight", terrainHeight);
        
        int threadGroups = Mathf.CeilToInt(instanceCount / 64f);
        grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
        
        args[1] = (uint)instanceCount;
        argsBuffer.SetData(args);
        
        lastInstanceCount = instanceCount;
    }
    
    
    void RenderGrass()
    {
        if (instanceBuffer == null || lastInstanceCount == 0) return;

        grassMaterial.SetBuffer("instanceData", instanceBuffer);
        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            renderBounds,
            argsBuffer,
            0,
            null,
            gameSettings.enableGrassShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }
    
    void OnDestroy()
    {
        instanceBuffer?.Release();
        heightMapBuffer?.Release();
        argsBuffer?.Release();
    }
}