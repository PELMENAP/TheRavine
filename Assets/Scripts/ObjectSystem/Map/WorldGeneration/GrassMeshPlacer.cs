using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class ChunkGrassSystem : MonoBehaviour
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
    [SerializeField] private int maxGrassInstances = 100000;
    [SerializeField] private float scaleMin = 0.8f;
    [SerializeField] private float scaleMax = 1.2f;
    [SerializeField] private float rotationVariation = 360f;
    
    [Header("Culling")]
    [SerializeField] private float cullingDistance = 100f;
    
    private ComputeBuffer instanceBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer visibleCountBuffer;
    
    private int kernelPlaceGrass;
    private Bounds renderBounds;
    private uint[] args = new uint[5];
    
    private int lastInstanceCount;
    
    void Start()
    {
        InitializeSystem();
    }
    private Vector3 oldPosition, offset = new Vector3(40, 0, 40);
    void Update()
    {
        if (targetMeshFilter == null || targetTransform == null) return;

        if(targetTransform.position != oldPosition)
        {
            UpdateGrassPlacement();
            oldPosition = targetTransform.position;
        }
        
        RenderGrass();
    }
    
    void InitializeSystem()
    {
        if (grassPlacementShader == null)
        {
            Debug.LogError("Compute shader not assigned!");
            return;
        }
        
        kernelPlaceGrass = grassPlacementShader.FindKernel("PlaceGrass");
        
        instanceBuffer = new ComputeBuffer(maxGrassInstances, 80);
        argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        
        args[0] = grassMesh.GetIndexCount(0);
        args[1] = 0;
        args[2] = grassMesh.GetIndexStart(0);
        args[3] = grassMesh.GetBaseVertex(0);
        args[4] = 0;
        
        renderBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }
    
    void UpdateGrassPlacement()
    {
        Mesh mesh = targetMeshFilter.sharedMesh;
        if (mesh == null) return;
        
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var meshBounds = mesh.bounds;
        
        Matrix4x4 localToWorld = targetTransform.localToWorldMatrix;
        
        Bounds worldBounds = TransformBounds(meshBounds, localToWorld);
        
        int minX = Mathf.FloorToInt(worldBounds.min.x);
        int maxX = Mathf.CeilToInt(worldBounds.max.x);
        int minZ = Mathf.FloorToInt(worldBounds.min.z);
        int maxZ = Mathf.CeilToInt(worldBounds.max.z);
        
        int gridWidth = maxX - minX;
        int gridHeight = maxZ - minZ;
        int totalGridPoints = gridWidth * gridHeight;
        
        int instanceCount = Mathf.Min(totalGridPoints, maxGrassInstances);
        
        if (triangleBuffer == null || triangleBuffer.count != triangles.Length / 3)
        {
            triangleBuffer?.Release();
            triangleBuffer = new ComputeBuffer(triangles.Length / 3, 60);
        }
        
        NativeArray<TriangleData> triangleData = new NativeArray<TriangleData>(triangles.Length / 3, Allocator.Temp);
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 v1 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 v2 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);
            
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            Vector3 center = (v0 + v1 + v2) / 3f;
            
            triangleData[i / 3] = new TriangleData
            {
                v0 = v0,
                v1 = v1,
                v2 = v2,
                normal = normal,
                center = center
            };
        }
        
        triangleBuffer.SetData(triangleData);
        triangleData.Dispose();
        
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "instanceData", instanceBuffer);
        grassPlacementShader.SetBuffer(kernelPlaceGrass, "triangles", triangleBuffer);
        grassPlacementShader.SetInt("triangleCount", triangles.Length / 3);
        grassPlacementShader.SetInt("instanceCount", instanceCount);
        grassPlacementShader.SetInt("gridMinX", minX);
        grassPlacementShader.SetInt("gridMinZ", minZ);
        grassPlacementShader.SetInt("gridWidth", gridWidth);
        grassPlacementShader.SetInt("gridHeight", gridHeight);
        grassPlacementShader.SetFloat("density", grassDensity);
        grassPlacementShader.SetFloat("scaleMin", scaleMin);
        grassPlacementShader.SetFloat("scaleMax", scaleMax);
        grassPlacementShader.SetFloat("rotationVariation", rotationVariation);
        grassPlacementShader.SetVector("worldBoundsMin", worldBounds.min);
        grassPlacementShader.SetVector("worldBoundsMax", worldBounds.max);
        grassPlacementShader.SetFloat("cullingDistance", cullingDistance);
        
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
            ShadowCastingMode.On,
            true,
            gameObject.layer
        );
    }
    
    Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center) + offset;
        Vector3 extents = localBounds.extents - offset;
        
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
        
        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
        
        return new Bounds(center, extents * 2f);
    }
    
    void OnDestroy()
    {
        instanceBuffer?.Release();
        triangleBuffer?.Release();
        argsBuffer?.Release();
        visibleCountBuffer?.Release();
    }
    
    void OnDrawGizmosSelected()
    {
        if (targetTransform != null && targetMeshFilter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = targetTransform.localToWorldMatrix;
            Gizmos.DrawWireCube(targetMeshFilter.sharedMesh.bounds.center + offset, 
                targetMeshFilter.sharedMesh.bounds.size);
        }
    }
    
    struct TriangleData
    {
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;
        public Vector3 normal;
        public Vector3 center;
    }
}