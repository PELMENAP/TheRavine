using TheRavine.Generator;
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
    [SerializeField] private Vector3 scaleMin = new(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 scaleMax = new(1.2f, 1.2f, 1.2f);
    
    [Header("Rotation")]
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private float maxYRotation = 180f;
    
    [Header("Compute Shaders")]
    [SerializeField] private ComputeShader meshSamplerShader;
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
        
        triangleBuffer = triangleBuffer = new ComputeBuffer(triangleData.Length, sizeof(float) * 24);
        triangleBuffer.SetData(triangleData);
        
        renderBounds = new Bounds(new Vector2(targetTransform.position.x + MapGenerator.generationSize, 
            targetTransform.position.y + MapGenerator.generationSize), 2f * cullingDistance * Vector3.one);
        
        kernelMeshSampling = meshSamplerShader.FindKernel("SampleMeshSurface");
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

        instanceDataBuffer.GetData(new InstanceData[1]); // форсируем синхронизацию
    }
    
    private Vector3 oldPosition;
    private void Update()
    {
        if(oldPosition != targetTransform.position)
        {
            OnDestroy();
            InitializeBuffers();
            GenerateGrassInstances();
            oldPosition = targetTransform.position;
        }

        RenderAllInstances();
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

        argsBuffer?.Dispose();
        instanceDataBuffer?.Dispose();
        visibleInstanceBuffer?.Dispose();
        triangleBuffer?.Dispose();
        counterBuffer?.Dispose();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
        }
    }
    
    private struct TriangleData
    {
        public Vector3 v0; private float _padding0;
        public Vector3 v1; private float _padding1;
        public Vector3 v2; private float _padding2;
        public Vector3 n0; private float _padding3;
        public Vector3 n1; private float _padding4;
        public Vector3 n2; private float _padding5;
    }
}