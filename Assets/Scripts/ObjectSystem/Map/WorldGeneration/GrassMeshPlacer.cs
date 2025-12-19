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
    [SerializeField] private float density = 1f;
    
    [Header("Scale Variation")]
    [SerializeField] private Vector3 scaleMin = new Vector3(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 scaleMax = new Vector3(1.2f, 1.2f, 1.2f);
    
    [Header("Rotation")]
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private float maxYRotation = 180f;
    
    [Header("Compute Shaders")]
    [SerializeField] private ComputeShader meshSamplerShader;
    [SerializeField] private ComputeShader cullingShader;
    
    [Header("Culling")]
    [SerializeField] private Camera cullingCamera;
    [SerializeField] private bool enableFrustumCulling = true;
    [SerializeField] private float cullingDistance = 100f;
    
    [Header("Performance")]
    [SerializeField] private bool castShadows = true;
    [SerializeField] private bool receiveShadows = true;
    
    private ComputeBuffer argsBuffer;
    private ComputeBuffer instanceDataBuffer;
    private ComputeBuffer visibleInstanceBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer counterBuffer;
    
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private int kernelMeshSampling;
    private int kernelCulling;
    private Bounds renderBounds;
    
    private const int THREAD_GROUP_SIZE = 64;
    
    private struct InstanceData
    {
        public Matrix4x4 trs;
        public Vector4 color;
    }
    
    private void Start()
    {
        InitializeBuffers();
        GenerateGrassInstances();
    }
    
    private void InitializeBuffers()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter = GetComponent<MeshFilter>();
            targetTransform = transform;
        }
        
        if (cullingCamera == null)
            cullingCamera = Camera.main;
        
        Mesh mesh = targetMeshFilter.sharedMesh;
        
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
        
        instanceDataBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16 + sizeof(float) * 4);
        visibleInstanceBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16 + sizeof(float) * 4, ComputeBufferType.Append);
        counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3[] normals = mesh.normals;
        
        TriangleData[] triangleData = new TriangleData[triangles.Length / 3];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int idx = i / 3;
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];
            
            Vector3 worldV0 = targetTransform.TransformPoint(vertices[i0]);
            Vector3 worldV1 = targetTransform.TransformPoint(vertices[i1]);
            Vector3 worldV2 = targetTransform.TransformPoint(vertices[i2]);
            
            Vector3 worldN0 = targetTransform.TransformDirection(normals[i0]);
            Vector3 worldN1 = targetTransform.TransformDirection(normals[i1]);
            Vector3 worldN2 = targetTransform.TransformDirection(normals[i2]);
            
            triangleData[idx] = new TriangleData
            {
                v0 = worldV0,
                v1 = worldV1,
                v2 = worldV2,
                n0 = worldN0,
                n1 = worldN1,
                n2 = worldN2
            };
        }
        
        triangleBuffer = new ComputeBuffer(triangleData.Length, sizeof(float) * 18);
        triangleBuffer.SetData(triangleData);
        
        renderBounds = new Bounds(targetTransform.position, Vector3.one * cullingDistance * 2f);
        
        kernelMeshSampling = meshSamplerShader.FindKernel("SampleMeshSurface");
        kernelCulling = cullingShader.FindKernel("FrustumCull");
    }
    
    private void GenerateGrassInstances()
    {
        meshSamplerShader.SetBuffer(kernelMeshSampling, "triangles", triangleBuffer);
        meshSamplerShader.SetBuffer(kernelMeshSampling, "instanceData", instanceDataBuffer);
        meshSamplerShader.SetInt("instanceCount", instanceCount);
        meshSamplerShader.SetInt("triangleCount", triangleBuffer.count);
        meshSamplerShader.SetVector("scaleMin", scaleMin);
        meshSamplerShader.SetVector("scaleMax", scaleMax);
        meshSamplerShader.SetFloat("density", density);
        meshSamplerShader.SetBool("alignToNormal", alignToSurfaceNormal);
        meshSamplerShader.SetBool("randomYRotation", randomYRotation);
        meshSamplerShader.SetFloat("maxYRotation", maxYRotation);
        meshSamplerShader.SetInt("randomSeed", (int)(Time.realtimeSinceStartup * 1000));
        
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)THREAD_GROUP_SIZE);
        meshSamplerShader.Dispatch(kernelMeshSampling, threadGroups, 1, 1);
    }
    
    private void Update()
    {
        if (enableFrustumCulling && cullingCamera != null)
        {
            PerformCulling();
        }
        else
        {
            RenderAllInstances();
        }
    }
    
    private void PerformCulling()
    {
        visibleInstanceBuffer.SetCounterValue(0);
        
        Matrix4x4 vp = GL.GetGPUProjectionMatrix(cullingCamera.projectionMatrix, false) * cullingCamera.worldToCameraMatrix;
        
        cullingShader.SetMatrix("vpMatrix", vp);
        cullingShader.SetBuffer(kernelCulling, "instanceData", instanceDataBuffer);
        cullingShader.SetBuffer(kernelCulling, "visibleInstances", visibleInstanceBuffer);
        cullingShader.SetInt("instanceCount", instanceCount);
        cullingShader.SetVector("cameraPosition", cullingCamera.transform.position);
        cullingShader.SetFloat("maxDistance", cullingDistance);
        
        int threadGroups = Mathf.CeilToInt(instanceCount / (float)THREAD_GROUP_SIZE);
        cullingShader.Dispatch(kernelCulling, threadGroups, 1, 1);
        
        ComputeBuffer.CopyCount(visibleInstanceBuffer, argsBuffer, sizeof(uint));
        
        grassMaterial.SetBuffer("instanceData", visibleInstanceBuffer);
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
    
    private void RenderAllInstances()
    {
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
        visibleInstanceBuffer?.Release();
        triangleBuffer?.Release();
        counterBuffer?.Release();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && cullingCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cullingCamera.transform.position, cullingDistance);
        }
    }
    
    private struct TriangleData
    {
        public Vector3 v0, v1, v2;
        public Vector3 n0, n1, n2;
    }
}