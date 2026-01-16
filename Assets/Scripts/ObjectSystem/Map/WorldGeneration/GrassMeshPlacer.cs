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
    
    [Header("Frustum Culling")]
    [SerializeField] private Camera cullingCamera;
    [SerializeField] private bool enableFrustumCulling = true;
    [SerializeField] private float cullingMargin = 2.0f;
    
    [Header("Performance")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;
    
    [Header("World Grid")]
    [SerializeField] private float gridCellSize = 1f;
    
    private ComputeBuffer argsBuffer;
    private ComputeBuffer instanceDataBuffer;
    private ComputeBuffer culledInstanceDataBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer indexBuffer;
    private ComputeBuffer visibleCountBuffer;
    private uint[] args = new uint[5];
    private int kernelPlaceGrass;
    private int kernelCullGrass;
    private Bounds renderBounds;
    private Vector3 cachedPosition;
    private Camera mainCamera;
    
    private const int THREAD_GROUP_SIZE = 64;
    
    private struct InstanceData
    {
        public Matrix4x4 trs;
        public Vector4 color;
        public Vector4 boundsInfo; // xyz = center, w = radius
    }
    
    private struct VertexData
    {
        public Vector3 position;
    }
    
    private void Start()
    {
        if (cullingCamera == null)
            cullingCamera = Camera.main;
        
        mainCamera = Camera.main;
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
        UpdateArgsBuffer(0);
        
        instanceDataBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16 + sizeof(float) * 4 + sizeof(float) * 4);
        culledInstanceDataBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16 + sizeof(float) * 4, ComputeBufferType.Append);
        visibleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        
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
        
        renderBounds = new Bounds(worldCenter, worldSize * 1.5f);
        
        kernelPlaceGrass = meshSamplerShader.FindKernel("PlaceGrass");
        kernelCullGrass = meshSamplerShader.FindKernel("CullGrass");
        
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceDataBuffer);
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "vertices", vertexBuffer);
        meshSamplerShader.SetBuffer(kernelPlaceGrass, "indices", indexBuffer);
        meshSamplerShader.SetInt("instanceCount", instanceCount);
        meshSamplerShader.SetInt("triangleCount", indices.Length / 3);
        meshSamplerShader.SetVector("scaleMin", scaleMin);
        meshSamplerShader.SetVector("scaleMax", scaleMax);
        meshSamplerShader.SetBool("randomYRotation", randomYRotation);
        meshSamplerShader.SetFloat("maxYRotation", maxYRotation);
        meshSamplerShader.SetFloat("gridCellSize", gridCellSize);
        
        Vector2 xzMin = new Vector2(worldCenter.x - worldSize.x * 0.5f, worldCenter.z - worldSize.z * 0.5f);
        Vector2 xzMax = new Vector2(worldCenter.x + worldSize.x * 0.5f, worldCenter.z + worldSize.z * 0.5f);
        meshSamplerShader.SetVector("xzBoundsMin", xzMin);
        meshSamplerShader.SetVector("xzBoundsMax", xzMax);
    }
    
    private void GenerateGrassInstances()
    {
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)THREAD_GROUP_SIZE);
        meshSamplerShader.Dispatch(kernelPlaceGrass, threadGroups, 1, 1);
    }
    
    private void UpdateArgsBuffer(uint instanceCount)
    {
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = instanceCount;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);
    }
    
    private void PerformFrustumCulling()
    {
        if (!enableFrustumCulling || cullingCamera == null)
        {
            // Если culling отключен, используем все экземпляры
            grassMaterial.SetBuffer("instanceData", instanceDataBuffer);
            UpdateArgsBuffer((uint)instanceCount);
            return;
        }
        
        // Получаем плоскости frustum камеры
        Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        Vector4[] planes = new Vector4[6];
        
        for (int i = 0; i < 6; i++)
        {
            planes[i] = new Vector4(cameraPlanes[i].normal.x, 
                                   cameraPlanes[i].normal.y, 
                                   cameraPlanes[i].normal.z, 
                                   cameraPlanes[i].distance);
        }
        
        culledInstanceDataBuffer.SetCounterValue(0);
        
        meshSamplerShader.SetBuffer(kernelCullGrass, "instanceData", instanceDataBuffer);
        meshSamplerShader.SetBuffer(kernelCullGrass, "culledInstanceData", culledInstanceDataBuffer);
        meshSamplerShader.SetInt("instanceCount", instanceCount);
        meshSamplerShader.SetFloat("cullingMargin", cullingMargin);
        
        meshSamplerShader.SetVector("plane0", planes[0]);
        meshSamplerShader.SetVector("plane1", planes[1]);
        meshSamplerShader.SetVector("plane2", planes[2]);
        meshSamplerShader.SetVector("plane3", planes[3]);
        meshSamplerShader.SetVector("plane4", planes[4]);
        meshSamplerShader.SetVector("plane5", planes[5]);
        
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)THREAD_GROUP_SIZE);
        meshSamplerShader.Dispatch(kernelCullGrass, threadGroups, 1, 1);
        
        ComputeBuffer.CopyCount(culledInstanceDataBuffer, argsBuffer, sizeof(uint));
        
        grassMaterial.SetBuffer("instanceData", culledInstanceDataBuffer);
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
        
        PerformFrustumCulling();
        
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
        culledInstanceDataBuffer?.Release();
        vertexBuffer?.Release();
        indexBuffer?.Release();
        visibleCountBuffer?.Release();
        
        argsBuffer?.Dispose();
        instanceDataBuffer?.Dispose();
        culledInstanceDataBuffer?.Dispose();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        visibleCountBuffer?.Dispose();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
            
            if (cullingCamera != null && enableFrustumCulling)
            {
                Gizmos.color = Color.red;
                Gizmos.matrix = cullingCamera.transform.localToWorldMatrix;
                Gizmos.DrawFrustum(Vector3.zero, cullingCamera.fieldOfView, 
                                 cullingCamera.farClipPlane, 
                                 cullingCamera.nearClipPlane, 
                                 cullingCamera.aspect);
            }
        }
    }
}