using UnityEngine;
using UnityEngine.Rendering;

public class GrassMeshPlacer : MonoBehaviour
{
    [Header("Target Mesh")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private Transform targetTransform;
    
    [Header("Grass Settings")]
    [SerializeField] private Mesh grassMesh;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private int instanceCount = 10000;
    
    [Header("Scale Variation")]
    [SerializeField] private Vector3 scaleMin = new(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 scaleMax = new(1.2f, 1.2f, 1.2f);
    
    [Header("Rotation")]
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private float maxYRotation = 180f;
    
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader meshSamplerShader;
    
    [Header("Performance")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;
    
    private ComputeBuffer argsBuffer;
    private ComputeBuffer instanceDataBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer indexBuffer;
    private uint[] args = new uint[5];
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private Vector3 cachedPosition;
    
    private const int THREAD_GROUP_SIZE = 64;
    
    private struct InstanceData
    {
        public Matrix4x4 trs;
        public Vector4 color;
    }
    
    private struct VertexData
    {
        public Vector3 position;
    }
    
    private void Start()
    {
        InitializeBuffers();
        GenerateGrassInstances();
        cachedPosition = targetTransform.position;
    }
    
    private void InitializeBuffers()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>();
            targetTransform = transform;
        }
        
        Mesh mesh = targetMeshFilter.sharedMesh;
        Bounds meshBounds = mesh.bounds;
        
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
        
        instanceDataBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16 + sizeof(float) * 4);
        
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        
        VertexData[] vertexData = new VertexData[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertexData[i].position = targetTransform.TransformPoint(vertices[i]);
        }
        
        vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertexData);
        
        indexBuffer = new ComputeBuffer(indices.Length, sizeof(int));
        indexBuffer.SetData(indices);
        
        Vector3 worldCenter = targetTransform.TransformPoint(meshBounds.center) + new Vector3(40, 0, 40);
        Vector3 worldSize = Vector3.Scale(meshBounds.size, targetTransform.lossyScale);
        
        renderBounds = new Bounds(worldCenter, worldSize);
        
        kernelPlaceGrass = meshSamplerShader.FindKernel("PlaceGrass");
        
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceDataBuffer);
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "vertices", vertexBuffer);
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "indices", indexBuffer);
        meshSamplerShader.SetInt("instanceCount", instanceCount);
        meshSamplerShader.SetInt("triangleCount", indices.Length / 3);
        meshSamplerShader.SetVector("scaleMin", scaleMin);
        meshSamplerShader.SetVector("scaleMax", scaleMax);
        meshSamplerShader.SetBool("randomYRotation", randomYRotation);
        meshSamplerShader.SetFloat("maxYRotation", maxYRotation);
        
        Vector2 xzMin = new Vector2(worldCenter.x - worldSize.x * 0.5f, worldCenter.z - worldSize.z * 0.5f);
        Vector2 xzMax = new Vector2(worldCenter.x + worldSize.x * 0.5f, worldCenter.z + worldSize.z * 0.5f);
        meshSamplerShader.SetVector("xzBoundsMin", xzMin);
        meshSamplerShader.SetVector("xzBoundsMax", xzMax);
    }
    
    private void GenerateGrassInstances()
    {
        meshSamplerShader.SetInt("randomSeed", (int)(Time.realtimeSinceStartup * 1000));
        
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)THREAD_GROUP_SIZE);
        meshSamplerShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
    }
    
    private void Update()
    {
        if (cachedPosition != targetTransform.position)
        {
            OnDestroy();
            InitializeBuffers();
            GenerateGrassInstances();
            cachedPosition = targetTransform.position;
        }

        grassMaterial.SetBuffer("instanceData", instanceDataBuffer);
        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            renderBounds,
            argsBuffer,
            0,
            null,
            castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            receiveShadows
        );
    }
    
    private void OnDestroy()
    {
        argsBuffer?.Release();
        instanceDataBuffer?.Release();
        vertexBuffer?.Release();
        indexBuffer?.Release();
        argsBuffer?.Dispose();
        instanceDataBuffer?.Dispose();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
        }
    }
}