using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ChunkGrassSystem : MonoBehaviour
{
    [Header("Target Mesh")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private Transform targetTransform;
    
    [Header("Grass Settings")]
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private int instancesPerChunk = 1000;
    
    [Header("Chunk Settings")]
    [SerializeField] private float chunkSize = 20f;
    
    [Header("Scale Variation")]
    [SerializeField] private Vector3 scaleMin = new(0.8f, 0.5f, 0.8f);
    [SerializeField] private Vector3 scaleMax = new(1.2f, 2f, 1.2f);
    [SerializeField] private bool useScaleNoise = true;
    [SerializeField] private float scaleNoiseScale = 0.05f;
    [SerializeField] private float scaleNoiseInfluence = 0.5f;
    
    [Header("Rotation")]
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private float maxYRotation = 180f;
    
    [Header("Density Control")]
    [SerializeField] private bool useDensityNoise = true;
    [SerializeField] private float noiseScale = 0.1f;
    [SerializeField] private float densityThreshold = 0.3f;
    [SerializeField] private int noiseOctaves = 2;
    [SerializeField] private float noiseLacunarity = 2.0f;
    [SerializeField] private float noisePersistence = 0.5f;
    
    [Header("Color Variation")]
    [SerializeField] private bool useHeightColorVariation = true;
    [SerializeField] private float minHeight = 0f;
    [SerializeField] private float maxHeight = 10f;
    [SerializeField] private Gradient heightColorGradient;
    [SerializeField] private bool useRandomColorTint = true;
    [SerializeField] private float colorVariation = 0.1f;
    
    [Header("Height Constraints")]
    [SerializeField] private bool useHeightConstraints = false;
    [SerializeField] private float minGenerationHeight = 0f;
    [SerializeField] private float maxGenerationHeight = 10f;
    
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader meshSamplerShader;
    
    [Header("Performance")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;
    [SerializeField] private bool debugDrawChunks = true;
    [SerializeField] private bool debugDrawMeshBounds = true;
    [SerializeField] private bool testSingleChunk = false;
    [SerializeField] private Vector2Int testChunkCoord = Vector2Int.zero;
    
    private readonly Dictionary<Vector2Int, GrassChunk> activeChunks = new();
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer indexBuffer;
    private int kernelPlaceGrass;
    private Mesh targetMesh;
    private Vector3[] targetVertices;
    private int[] targetIndices;
    
    private const int THREAD_GROUP_SIZE = 64;
    private static readonly int InstanceDataID = Shader.PropertyToID("instanceData");
    
    private struct InstanceData
    {
        public Matrix4x4 trs;
        public Vector4 color;
    }
    
    private struct VertexData
    {
        public Vector3 position;
    }
    
    private class GrassChunk
    {
        public Vector2Int coord;
        public ComputeBuffer instanceBuffer;
        public ComputeBuffer argsBuffer;
        public uint[] args = new uint[5];
        public Bounds bounds;
        public int instanceCount;
    }
    
    private void Start()
    {
        InitializeGradient();
        
        InitializeComputeShader();
        targetTransform ??= transform;

        UpdateChunks().Forget();
    }
    
    private void InitializeGradient()
    {
        if (heightColorGradient == null)
        {
            heightColorGradient = new Gradient();
            heightColorGradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.3f, 0.5f, 0.2f), 0f), 
                       new GradientColorKey(new Color(0.5f, 0.8f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
        }
    }
    
    private void InitializeComputeShader()
    {
        targetMesh = targetMeshFilter.sharedMesh;
        targetVertices = targetMesh.vertices;
        targetIndices = targetMesh.triangles;
        
        VertexData[] vertexData = new VertexData[targetVertices.Length];
        for (int i = 0; i < targetVertices.Length; i++)
            vertexData[i].position = targetVertices[i];
        
        vertexBuffer = new ComputeBuffer(targetVertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertexData);
        
        indexBuffer = new ComputeBuffer(targetIndices.Length, sizeof(int));
        indexBuffer.SetData(targetIndices);
        
        kernelPlaceGrass = meshSamplerShader.FindKernel("PlaceGrass");
        
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "vertices", vertexBuffer);
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "indices", indexBuffer);
        meshSamplerShader.SetInt("triangleCount", targetIndices.Length / 3);
        
        SetComputeShaderParameters();
    }
    
    private void SetComputeShaderParameters()
    {
        meshSamplerShader.SetVector("scaleMin", scaleMin);
        meshSamplerShader.SetVector("scaleMax", scaleMax);
        meshSamplerShader.SetBool("randomYRotation", randomYRotation);
        meshSamplerShader.SetFloat("maxYRotation", maxYRotation);
        meshSamplerShader.SetBool("useScaleNoise", useScaleNoise);
        meshSamplerShader.SetFloat("scaleNoiseScale", scaleNoiseScale);
        meshSamplerShader.SetFloat("scaleNoiseInfluence", scaleNoiseInfluence);
        meshSamplerShader.SetBool("useDensityNoise", useDensityNoise);
        meshSamplerShader.SetFloat("noiseScale", noiseScale);
        meshSamplerShader.SetFloat("densityThreshold", densityThreshold);
        meshSamplerShader.SetInt("noiseOctaves", noiseOctaves);
        meshSamplerShader.SetFloat("noiseLacunarity", noiseLacunarity);
        meshSamplerShader.SetFloat("noisePersistence", noisePersistence);
        meshSamplerShader.SetBool("useHeightColorVariation", useHeightColorVariation);
        meshSamplerShader.SetFloat("minHeight", minHeight);
        meshSamplerShader.SetFloat("maxHeight", maxHeight);
        meshSamplerShader.SetBool("useRandomColorTint", useRandomColorTint);
        meshSamplerShader.SetFloat("colorVariation", colorVariation);
        meshSamplerShader.SetBool("useHeightConstraints", useHeightConstraints);
        meshSamplerShader.SetFloat("minGenerationHeight", minGenerationHeight);
        meshSamplerShader.SetFloat("maxGenerationHeight", maxGenerationHeight);
        
        SetGradientToComputeShader();
    }
    
    private void SetGradientToComputeShader()
    {
        const int gradientResolution = 64;
        Vector4[] gradientData = new Vector4[gradientResolution];
        
        for (int i = 0; i < gradientResolution; i++)
        {
            float t = i / (float)(gradientResolution - 1);
            Color col = heightColorGradient.Evaluate(t);
            gradientData[i] = new Vector4(col.r, col.g, col.b, col.a);
        }
        
        meshSamplerShader.SetInt("gradientResolution", gradientResolution);
        meshSamplerShader.SetVectorArray("heightGradient", gradientData);
    }
    
    private void Update()
    {
        //     UpdateChunks().Forget();
        
        RenderChunks();
    }
    
    private Vector3 offset = new Vector3(40, 0, 40);
    private async UniTaskVoid UpdateChunks()
    {
        Bounds meshBounds = targetMesh.bounds;
        Vector3 meshMin = targetTransform.TransformPoint(meshBounds.min + offset) + offset;
        Vector3 meshMax = targetTransform.TransformPoint(meshBounds.max - offset) + offset;
        
        Vector2Int minChunk = WorldToChunkCoord(new Vector2(meshMin.x, meshMin.z));
        Vector2Int maxChunk = WorldToChunkCoord(new Vector2(meshMax.x, meshMax.z));
        
        HashSet<Vector2Int> requiredChunks = new();
        for (int x = minChunk.x; x <= maxChunk.x; x++)
        {
            for (int z = minChunk.y; z <= maxChunk.y; z++)
            {
                Vector2Int chunkCoord = new(x, z);
                if (IsChunkFullyInMesh(chunkCoord, meshMin, meshMax))
                    requiredChunks.Add(chunkCoord);
            }
        }
        
        List<Vector2Int> chunksToRemove = new();
        foreach (var coord in activeChunks.Keys)
        {
            if (!requiredChunks.Contains(coord))
                chunksToRemove.Add(coord);
        }
        
        foreach (var coord in chunksToRemove)
        {
            DestroyChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }
        
        foreach (var coord in requiredChunks)
        {
            if (!activeChunks.ContainsKey(coord))
            {
                await UniTask.Delay(1000);
                GrassChunk chunk = CreateChunk(coord);
                if (chunk != null)
                    activeChunks[coord] = chunk;
            }
        }
        
    }
    
    private Vector2Int WorldToChunkCoord(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.y / chunkSize)
        );
    }
    
    private Vector2 ChunkCoordToWorld(Vector2Int chunkCoord)
    {
        return new Vector2(chunkCoord.x * chunkSize, chunkCoord.y * chunkSize);
    }
    
    private bool IsChunkFullyInMesh(Vector2Int chunkCoord, Vector3 meshMin, Vector3 meshMax)
    {
        Vector2 chunkMin = ChunkCoordToWorld(chunkCoord);
        Vector2 chunkMax = chunkMin + Vector2.one * chunkSize;
        
        bool intersects = !(chunkMax.x < meshMin.x || chunkMin.x > meshMax.x ||
                           chunkMax.y < meshMin.z || chunkMin.y > meshMax.z);
        
        
        return intersects;
    }
    
    private GrassChunk CreateChunk(Vector2Int coord)
    {
        
        GrassChunk chunk = new()
        {
            coord = coord,
            instanceCount = instancesPerChunk
        };
        
        int stride = sizeof(float) * 16 + sizeof(float) * 4;
        chunk.instanceBuffer = new ComputeBuffer(instancesPerChunk, stride);
        chunk.argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        chunk.args[0] = grassMesh.GetIndexCount(0);
        chunk.args[1] = (uint)instancesPerChunk;
        chunk.args[2] = grassMesh.GetIndexStart(0);
        chunk.args[3] = grassMesh.GetBaseVertex(0);
        chunk.args[4] = 0;
        chunk.argsBuffer.SetData(chunk.args);
        
        
        Vector2 chunkWorldMin = ChunkCoordToWorld(coord);
        Vector2 chunkWorldMax = chunkWorldMin + Vector2.one * chunkSize;
        
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "instanceData", chunk.instanceBuffer);
        meshSamplerShader.SetInt("instanceCount", instancesPerChunk);
        meshSamplerShader.SetVector("chunkWorldMin", chunkWorldMin);
        meshSamplerShader.SetVector("chunkWorldMax", chunkWorldMax);
        meshSamplerShader.SetFloat("chunkSize", chunkSize);
        meshSamplerShader.SetVector("meshLocalToWorld_Position", targetTransform.position);
        meshSamplerShader.SetVector("meshLocalToWorld_Scale", targetTransform.lossyScale);
        
        Matrix4x4 worldToLocal = targetTransform.worldToLocalMatrix;
        meshSamplerShader.SetMatrix("meshWorldToLocal", worldToLocal);
        
        int threadGroups = Mathf.CeilToInt(instancesPerChunk / (float)THREAD_GROUP_SIZE);
        meshSamplerShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
        
        Vector3 center = new(
            chunkWorldMin.x + chunkSize * 0.5f,
            (minHeight + maxHeight) * 0.5f,
            chunkWorldMin.y + chunkSize * 0.5f
        );
        Vector3 size = new(chunkSize, maxHeight - minHeight, chunkSize);
        chunk.bounds = new Bounds(center, size);

        return chunk;
    }
    
    private void DestroyChunk(GrassChunk chunk)
    {
        chunk.instanceBuffer?.Release();
        chunk.argsBuffer?.Release();
    }
    
    private void RenderChunks()
    {
        foreach (var chunk in activeChunks.Values)
        {
            grassMaterial.SetBuffer(InstanceDataID, chunk.instanceBuffer);
            
            Graphics.DrawMeshInstancedIndirect(
                grassMesh,
                0,
                grassMaterial,
                chunk.bounds,
                chunk.argsBuffer,
                0,
                null,
                castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows
            );
        }
    }
    
    private void OnDisable()
    {
        foreach (var chunk in activeChunks.Values)
            DestroyChunk(chunk);
        
        activeChunks.Clear();
        vertexBuffer?.Release();
        indexBuffer?.Release();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[ChunkGrassSystem] {message}");
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        if (debugDrawChunks)
        {
            Gizmos.color = Color.green;
            foreach (var chunk in activeChunks.Values)
            {
                Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);
                
                Vector2 chunkWorld = ChunkCoordToWorld(chunk.coord);
                Vector3 labelPos = new(chunkWorld.x + chunkSize * 0.5f, chunk.bounds.center.y, chunkWorld.y + chunkSize * 0.5f);
                
#if UNITY_EDITOR
                UnityEditor.Handles.Label(labelPos, $"Chunk {chunk.coord}\n{chunk.instanceCount} instances");
#endif
            }
        }
        
        if (debugDrawMeshBounds && targetMeshFilter != null && targetTransform != null)
        {
            Bounds meshBounds = targetMesh.bounds;
            Vector3 meshMin = targetTransform.TransformPoint(meshBounds.min);
            Vector3 meshMax = targetTransform.TransformPoint(meshBounds.max);
            
            Gizmos.color = Color.yellow;
            Vector3 center = (meshMin + meshMax) * 0.5f;
            Vector3 size = meshMax - meshMin;
            Gizmos.DrawWireCube(center, size);
        }
    }
}