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
    
    private const int terrainResolution = 1 + 3 * MapGenerator.mapChunkSize;
    private const int vertexCount = terrainResolution * terrainResolution;
    private const int maxGrassInstances = 300000;
    
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer argsBuffer;
    
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private readonly uint[] args = new uint[5];
    
    private int instanceCount;

    private int densityFactor;
    private bool isGrass, isShadows;
    private NativeArray<float> heightMap;

    private Vector3 specialOffset = new(MapGenerator.mapChunkSize, 0, MapGenerator.mapChunkSize);
    private Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center) + specialOffset;
        Vector3 extents = localBounds.extents;
        
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
        
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
        
        return new Bounds(center, extents * 2f);
    }
    
    private void Start()
    {
        heightMap = new(vertexCount, Allocator.Persistent);
        try
        {
            var gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            densityFactor = gameSettings.grassDensityFactor;
            isGrass = gameSettings.enableGrass;
            isShadows = gameSettings.enableGrassShadows;
        }
        catch (System.Exception)
        {
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
        
        instanceBuffer = new ComputeBuffer(maxGrassInstances * densityFactor, 28);
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

        grassPlacementShader.SetFloat("globalMinHeight", globalMinHeight);
        grassPlacementShader.SetFloat("globalMaxHeight", globalMaxHeight);
        
        // grassPlacementShader.SetInt("occlusionMinX", MapGenerator.mapChunkSize);
        // grassPlacementShader.SetInt("occlusionMaxX", MapGenerator.mapChunkSize * 5);

        int gridWidth = MapGenerator.mapChunkSize * 6;
        int gridHeight = MapGenerator.mapChunkSize * 6;
        int totalGridPoints = gridWidth * gridHeight * bladesPerCell * densityFactor;
        instanceCount = Mathf.Min(totalGridPoints, maxGrassInstances * densityFactor);

        args[1] = (uint)instanceCount;
        argsBuffer.SetData(args);
        
        float terrainWidth = MapGenerator.mapChunkSize * 6;
        float terrainHeight = MapGenerator.mapChunkSize * 6;
        
        grassPlacementShader.SetInt("instanceCount", instanceCount);

        grassPlacementShader.SetInt("gridWidth", gridWidth);
        grassPlacementShader.SetInt("gridHeight", gridHeight);

        grassPlacementShader.SetFloat("terrainInvWidth",  1f / terrainWidth);
        grassPlacementShader.SetFloat("terrainInvHeight", 1f / terrainHeight);

        heightMapBuffer?.Release();
        heightMapBuffer = new ComputeBuffer(vertexCount, sizeof(float));
    }
    
    public void UpdateGrassPlacement()
    {
        isGrass = false;

        Mesh mesh = targetMeshFilter.sharedMesh;
        if (mesh == null) return;
        
        Vector3[] vertices = mesh.vertices;
        Bounds meshBounds = mesh.bounds;
        Matrix4x4 localToWorld = targetTransform.localToWorldMatrix;
        
        
        for (int k = 0; k < vertexCount; k++)
        {
            int i = k / terrainResolution;
            int j = k % terrainResolution;

            heightMap[k] = vertices[j * terrainResolution + i].y;
        }
        
        heightMapBuffer.SetData(heightMap);
        
        Bounds worldBounds = TransformBounds(meshBounds, localToWorld);
        
        int minX = Mathf.FloorToInt(worldBounds.min.x);
        int minZ = Mathf.FloorToInt(worldBounds.min.z);
        
        
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "heightMap", heightMapBuffer);
        
        grassPlacementShader.SetInt("gridMinX", minX);
        grassPlacementShader.SetInt("gridMinZ", minZ);

        grassPlacementShader.SetVector("worldBoundsMin", worldBounds.min);
        grassPlacementShader.SetVector("worldBoundsMax", worldBounds.max);
        
        int threadGroups = Mathf.CeilToInt(instanceCount / 64f);
        grassPlacementShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);

        isGrass = true;
    }
    
    
    private void RenderGrass()
    {
        if (instanceBuffer == null) return;

        grassMaterial.SetBuffer("instanceData", instanceBuffer);
        
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
        argsBuffer?.Release();

        heightMap.Dispose();
    }
}