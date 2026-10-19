using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct BoidGpu
{
    public Vector3 position;
    public float phase;
    public Vector3 velocity;
    public float bank;
}

public sealed class BoidsMenuBackground : MonoBehaviour
{
    private const int ThreadGroup = 64;

    private static readonly int BoidsInId = Shader.PropertyToID("boidsIn");
    private static readonly int BoidsOutId = Shader.PropertyToID("boidsOut");
    private static readonly int CellCountId = Shader.PropertyToID("cellCount");
    private static readonly int CellItemsId = Shader.PropertyToID("cellItems");
    private static readonly int BoidCountId = Shader.PropertyToID("boidCount");
    private static readonly int CellTotalId = Shader.PropertyToID("cellTotal");
    private static readonly int CellCapId = Shader.PropertyToID("cellCap");
    private static readonly int MaxNeighborsId = Shader.PropertyToID("maxNeighbors");
    private static readonly int GridDimsId = Shader.PropertyToID("gridDims");
    private static readonly int ExtentsId = Shader.PropertyToID("boundsExtents");
    private static readonly int TargetId = Shader.PropertyToID("target");
    private static readonly int CellInvId = Shader.PropertyToID("cellInv");
    private static readonly int Perception2Id = Shader.PropertyToID("perception2");
    private static readonly int Separation2Id = Shader.PropertyToID("separation2");
    private static readonly int WSepId = Shader.PropertyToID("wSep");
    private static readonly int WAliId = Shader.PropertyToID("wAli");
    private static readonly int WCohId = Shader.PropertyToID("wCoh");
    private static readonly int WBoundsId = Shader.PropertyToID("wBounds");
    private static readonly int WTargetId = Shader.PropertyToID("wTarget");
    private static readonly int WNoiseId = Shader.PropertyToID("wNoise");
    private static readonly int MinSpeedId = Shader.PropertyToID("minSpeed");
    private static readonly int MaxSpeedId = Shader.PropertyToID("maxSpeed");
    private static readonly int MaxSteerId = Shader.PropertyToID("maxSteer");
    private static readonly int DtId = Shader.PropertyToID("dt");
    private static readonly int TimeId = Shader.PropertyToID("time");
    private static readonly int RenderBoidsId = Shader.PropertyToID("_Boids");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RenderTimeId = Shader.PropertyToID("_Tm");

    [SerializeField] private ComputeShader compute;
    [SerializeField] private Material material;
    [SerializeField, Min(64)] private int count = 8192;
    [SerializeField] private Vector3 extents = new(40f, 20f, 30f);
    [SerializeField, Min(0.5f)] private float perception = 3f;
    [SerializeField, Min(0.1f)] private float separationRadius = 1.1f;
    [SerializeField, Range(4, 64)] private int maxNeighbors = 24;
    [SerializeField, Range(16, 128)] private int cellCapacity = 48;
    [SerializeField] private float separationWeight = 3f;
    [SerializeField] private float alignmentWeight = 1.2f;
    [SerializeField] private float cohesionWeight = 0.6f;
    [SerializeField] private float boundsWeight = 6f;
    [SerializeField] private float targetWeight = 1.5f;
    [SerializeField] private float noiseWeight = 1.5f;
    [SerializeField] private Vector2 speedRange = new(4f, 9f);
    [SerializeField] private float maxSteer = 14f;
    [SerializeField] private float targetSpeed = 0.15f;
    [SerializeField] private Vector3 targetRadius = new(25f, 10f, 18f);
    [SerializeField] private int seed = 1234;

    private readonly GraphicsBuffer[] boids = new GraphicsBuffer[2];
    private GraphicsBuffer cellCountBuffer;
    private GraphicsBuffer cellItemsBuffer;
    private GraphicsBuffer argsBuffer;
    private MaterialPropertyBlock props;
    private Mesh mesh;
    private int kernelClear;
    private int kernelBuild;
    private int kernelStep;
    private int cellTotal;
    private int current;
    private int boidGroups;
    private int cellGroups;
    private float simTime;

    private void OnEnable()
    {
        kernelClear = compute.FindKernel("ClearGrid");
        kernelBuild = compute.FindKernel("BuildGrid");
        kernelStep = compute.FindKernel("Step");

        Vector3Int dims = new(
            Mathf.CeilToInt(extents.x * 2f / perception),
            Mathf.CeilToInt(extents.y * 2f / perception),
            Mathf.CeilToInt(extents.z * 2f / perception));
        cellTotal = dims.x * dims.y * dims.z;
        boidGroups = Mathf.CeilToInt(count / (float)ThreadGroup);
        cellGroups = Mathf.CeilToInt(cellTotal / (float)ThreadGroup);

        int stride = Marshal.SizeOf<BoidGpu>();
        boids[0] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        boids[1] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        cellCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellTotal, sizeof(uint));
        cellItemsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellTotal * cellCapacity, sizeof(uint));

        mesh = BuildMesh();
        argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        argsBuffer.SetData(new[]
        {
            new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0),
                instanceCount = (uint)count
            }
        });

        Seed();

        compute.SetInt(BoidCountId, count);
        compute.SetInt(CellTotalId, cellTotal);
        compute.SetInt(CellCapId, cellCapacity);
        compute.SetInt(MaxNeighborsId, maxNeighbors);
        compute.SetInts(GridDimsId, dims.x, dims.y, dims.z);
        compute.SetVector(ExtentsId, extents);
        compute.SetFloat(CellInvId, 1f / perception);
        compute.SetFloat(Perception2Id, perception * perception);
        compute.SetFloat(Separation2Id, separationRadius * separationRadius);
        compute.SetFloat(WSepId, separationWeight);
        compute.SetFloat(WAliId, alignmentWeight);
        compute.SetFloat(WCohId, cohesionWeight);
        compute.SetFloat(WBoundsId, boundsWeight);
        compute.SetFloat(WTargetId, targetWeight);
        compute.SetFloat(WNoiseId, noiseWeight);
        compute.SetFloat(MinSpeedId, speedRange.x);
        compute.SetFloat(MaxSpeedId, speedRange.y);
        compute.SetFloat(MaxSteerId, maxSteer);

        compute.SetBuffer(kernelClear, CellCountId, cellCountBuffer);
        compute.SetBuffer(kernelBuild, CellCountId, cellCountBuffer);
        compute.SetBuffer(kernelBuild, CellItemsId, cellItemsBuffer);
        compute.SetBuffer(kernelStep, CellCountId, cellCountBuffer);
        compute.SetBuffer(kernelStep, CellItemsId, cellItemsBuffer);

        props = new MaterialPropertyBlock();
        current = 0;
        simTime = 0f;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        simTime += dt;

        Vector3 target = new(
            Mathf.Sin(simTime * targetSpeed) * targetRadius.x,
            Mathf.Sin(simTime * targetSpeed * 1.7f + 1f) * targetRadius.y,
            Mathf.Cos(simTime * targetSpeed * 1.3f) * targetRadius.z);

        compute.SetFloat(DtId, dt);
        compute.SetFloat(TimeId, simTime);
        compute.SetVector(TargetId, target);

        GraphicsBuffer read = boids[current];
        GraphicsBuffer write = boids[1 - current];

        compute.SetBuffer(kernelBuild, BoidsInId, read);
        compute.SetBuffer(kernelStep, BoidsInId, read);
        compute.SetBuffer(kernelStep, BoidsOutId, write);

        compute.Dispatch(kernelClear, cellGroups, 1, 1);
        compute.Dispatch(kernelBuild, boidGroups, 1, 1);
        compute.Dispatch(kernelStep, boidGroups, 1, 1);

        current = 1 - current;

        props.SetBuffer(RenderBoidsId, boids[current]);
        props.SetVector(CenterId, transform.position);
        props.SetFloat(RenderTimeId, simTime);

        RenderParams rp = new(material)
        {
            worldBounds = new Bounds(transform.position, extents * 2.5f),
            matProps = props,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false
        };
        Graphics.RenderMeshIndirect(rp, mesh, argsBuffer);
    }

    private void OnDisable()
    {
        boids[0]?.Release();
        boids[1]?.Release();
        cellCountBuffer?.Release();
        cellItemsBuffer?.Release();
        argsBuffer?.Release();
        boids[0] = null;
        boids[1] = null;
        cellCountBuffer = null;
        cellItemsBuffer = null;
        argsBuffer = null;
        if (mesh != null) Destroy(mesh);
    }

    private void Seed()
    {
        var data = new BoidGpu[count];
        var state = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < count; i++)
        {
            Vector3 p = Random.insideUnitSphere * 0.8f;
            data[i] = new BoidGpu
            {
                position = new Vector3(p.x * extents.x, p.y * extents.y, p.z * extents.z),
                phase = Random.value * Mathf.PI * 2f,
                velocity = Random.onUnitSphere * Mathf.Lerp(speedRange.x, speedRange.y, Random.value),
                bank = 0f
            };
        }

        Random.state = state;
        boids[0].SetData(data);
        boids[1].SetData(data);
    }

    private static Mesh BuildMesh()
    {
        var m = new Mesh { name = "BoidMesh" };
        m.vertices = new[]
        {
            new Vector3(0f, 0f, 0.6f),
            new Vector3(0f, 0f, -0.6f),
            new Vector3(-0.7f, 0f, -0.35f),
            new Vector3(0.7f, 0f, -0.35f),
            new Vector3(0f, 0.18f, -0.15f)
        };
        m.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f)
        };
        m.triangles = new[] { 0, 2, 1, 0, 1, 3, 0, 1, 4 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        m.UploadMeshData(true);
        return m;
    }
}