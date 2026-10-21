using System.Runtime.InteropServices;
using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

using TheRavine.Generator;

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
    private const int ScanBlock = 1024;
    private const int MaxScanLevels = 4;

    private static readonly int BoidsInId = Shader.PropertyToID("boidsIn");
    private static readonly int BoidsOutId = Shader.PropertyToID("boidsOut");
    private static readonly int CellStartId = Shader.PropertyToID("cellStart");
    private static readonly int BoidKeyId = Shader.PropertyToID("boidKey");
    private static readonly int ScanDataId = Shader.PropertyToID("scanData");
    private static readonly int ScanSumsId = Shader.PropertyToID("scanSums");
    private static readonly int ScanCountId = Shader.PropertyToID("scanCount");
    private static readonly int BoidCountId = Shader.PropertyToID("boidCount");
    private static readonly int CellTotalId = Shader.PropertyToID("cellTotal");
    private static readonly int MaxNeighborsId = Shader.PropertyToID("maxNeighbors");
    private static readonly int FrameId = Shader.PropertyToID("frame");
    private static readonly int GridDimsId = Shader.PropertyToID("gridDims");
    private static readonly int ExtentsId = Shader.PropertyToID("boundsExtents");
    private static readonly int TargetId = Shader.PropertyToID("target");
    private static readonly int CellInvId = Shader.PropertyToID("cellInv");
    private static readonly int Perception2Id = Shader.PropertyToID("perception2");
    private static readonly int Separation2Id = Shader.PropertyToID("separation2");
    private static readonly int SepMin2Id = Shader.PropertyToID("sepMin2");
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
    private static readonly int CursorOriginId = Shader.PropertyToID("cursorOrigin");
    private static readonly int CursorDirId = Shader.PropertyToID("cursorDir");
    private static readonly int CursorPrevOriginId = Shader.PropertyToID("cursorPrevOrigin");
    private static readonly int CursorPrevDirId = Shader.PropertyToID("cursorPrevDir");
    private static readonly int CursorParamsId = Shader.PropertyToID("cursorParams");
    private static readonly int ShockCenterId = Shader.PropertyToID("shockCenter");
    private static readonly int ShockParamsId = Shader.PropertyToID("shockParams");
    private static readonly int WindVelocityId = Shader.PropertyToID("windVelocity");
    private static readonly int RenderBoidsId = Shader.PropertyToID("_Boids");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RenderTimeId = Shader.PropertyToID("_Tm");

    [SerializeField] private ComputeShader compute;
    [SerializeField] private Material material;
    [SerializeField, Min(64)] private int count = 8192;
    [SerializeField] private Vector3 extents = new(40f, 20f, 30f);
    [SerializeField, Min(0.5f)] private float perception = 3f;
    [SerializeField, Min(0.1f)] private float separationRadius = 1.1f;
    [SerializeField, Min(0.01f)] private float separationMin = 0.25f;
    [SerializeField, Range(4, 64)] private int maxNeighbors = 24;
    [SerializeField] private float separationWeight = 3f;
    [SerializeField] private float alignmentWeight = 1.2f;
    [SerializeField] private float cohesionWeight = 0.6f;
    [SerializeField] private float boundsWeight = 6f;
    [SerializeField] private float targetWeight = 1.5f;
    [SerializeField] private float noiseWeight = 1.5f;
    [SerializeField] private Vector2 speedRange = new(4f, 9f);
    [SerializeField] private float maxSteer = 14f;
    [SerializeField] private float maxDeltaTime = 0.033f;
    [SerializeField] private float targetSpeed = 0.15f;
    [SerializeField] private Vector3 targetRadius = new(25f, 10f, 18f);
    [SerializeField] private int seed = 1234;

    [Header("Cursor")]
    [SerializeField] private float cursorRadius = 4f;
    [SerializeField] private float cursorSlope = 0.08f;
    [SerializeField] private float cursorStrength = 40f;
    [SerializeField] private float cursorTrail = 0.6f;
    [SerializeField] private float cursorMoveThreshold = 1f;
    [SerializeField] private float cursorIdleTime = 0.6f;
    [SerializeField] private float cursorFadeIn = 0.2f;
    [SerializeField] private float cursorFadeOut = 0.8f;
    [SerializeField] private Ease cursorEase = Ease.OutQuad;

    [Header("Shock Wave")]
    [SerializeField] private float shockMaxRadius = 40f;
    [SerializeField] private float shockWidth = 4f;
    [SerializeField] private float shockStrength = 60f;
    [SerializeField] private float shockDuration = 1.2f;
    [SerializeField] private Ease shockEase = Ease.OutCubic;

    [Header("Wind")]
    [SerializeField] private Vector2 windDirection = new(1f, 0f);
    [SerializeField] private float windStrength = 0.3f;
    [SerializeField] private float windDrift = 2f;

    private readonly GraphicsBuffer[] boids = new GraphicsBuffer[2];
    private readonly GraphicsBuffer[] scanLevels = new GraphicsBuffer[MaxScanLevels + 1];
    private readonly int[] scanCounts = new int[MaxScanLevels + 1];
    private GraphicsBuffer cellStartBuffer;
    private GraphicsBuffer boidKeyBuffer;
    private GraphicsBuffer argsBuffer;
    private MaterialPropertyBlock props;
    private Mesh mesh;
    private Camera cam;
    private WindField windField;
    private int kernelClear;
    private int kernelCount;
    private int kernelScan;
    private int kernelAdd;
    private int kernelScatter;
    private int kernelStep;
    private int cellTotal;
    private int scanLevelCount;
    private int boidGroups;
    private int cellGroups;
    private int frame;
    private float simTime;

    private Vector3 cursorOrigin, cursorDir, cursorPrevOrigin, cursorPrevDir;
    private Vector2 lastMouse;
    private float lastMoveTime;
    private float cursorFactor;
    private float cursorGoal;
    private MotionHandle cursorMotion;

    private Vector3 shockCenter;
    private float shockRadius;
    private MotionHandle shockMotion;

    private void OnEnable()
    {
        kernelClear = compute.FindKernel("ClearGrid");
        kernelCount = compute.FindKernel("CountGrid");
        kernelScan = compute.FindKernel("Scan");
        kernelAdd = compute.FindKernel("AddOffsets");
        kernelScatter = compute.FindKernel("Scatter");
        kernelStep = compute.FindKernel("Step");

        Vector3Int dims = new(
            Mathf.Max(1, Mathf.CeilToInt(extents.x * 2f / perception)),
            Mathf.Max(1, Mathf.CeilToInt(extents.y * 2f / perception)),
            Mathf.Max(1, Mathf.CeilToInt(extents.z * 2f / perception)));
        cellTotal = dims.x * dims.y * dims.z;
        boidGroups = Mathf.CeilToInt(count / (float)ThreadGroup);
        cellGroups = Mathf.CeilToInt((cellTotal + 1) / (float)ThreadGroup);

        int stride = Marshal.SizeOf<BoidGpu>();
        boids[0] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        boids[1] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
        cellStartBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellTotal + 1, sizeof(uint));
        boidKeyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(uint) * 2);
        BuildScanLevels(cellTotal + 1);

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
        compute.SetInt(MaxNeighborsId, maxNeighbors);
        compute.SetInts(GridDimsId, dims.x, dims.y, dims.z);
        compute.SetVector(ExtentsId, extents);
        compute.SetFloat(CellInvId, 1f / perception);
        compute.SetFloat(Perception2Id, perception * perception);
        compute.SetFloat(Separation2Id, separationRadius * separationRadius);
        compute.SetFloat(SepMin2Id, separationMin * separationMin);
        compute.SetFloat(WSepId, separationWeight);
        compute.SetFloat(WAliId, alignmentWeight);
        compute.SetFloat(WCohId, cohesionWeight);
        compute.SetFloat(WBoundsId, boundsWeight);
        compute.SetFloat(WTargetId, targetWeight);
        compute.SetFloat(WNoiseId, noiseWeight);
        compute.SetFloat(MinSpeedId, speedRange.x);
        compute.SetFloat(MaxSpeedId, speedRange.y);
        compute.SetFloat(MaxSteerId, maxSteer);

        compute.SetBuffer(kernelClear, CellStartId, cellStartBuffer);
        compute.SetBuffer(kernelCount, CellStartId, cellStartBuffer);
        compute.SetBuffer(kernelCount, BoidKeyId, boidKeyBuffer);
        compute.SetBuffer(kernelScatter, CellStartId, cellStartBuffer);
        compute.SetBuffer(kernelScatter, BoidKeyId, boidKeyBuffer);
        compute.SetBuffer(kernelStep, CellStartId, cellStartBuffer);

        compute.SetBuffer(kernelCount, BoidsInId, boids[0]);
        compute.SetBuffer(kernelScatter, BoidsInId, boids[0]);
        compute.SetBuffer(kernelScatter, BoidsOutId, boids[1]);
        compute.SetBuffer(kernelStep, BoidsInId, boids[1]);
        compute.SetBuffer(kernelStep, BoidsOutId, boids[0]);

        props = new MaterialPropertyBlock();
        props.SetBuffer(RenderBoidsId, boids[0]);

        cam = Camera.main;
        ServiceLocator.Services.TryGet(out windField);

        simTime = 0f;
        frame = 0;
        cursorFactor = 0f;
        cursorGoal = 0f;
        shockRadius = 0f;
        lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        UpdateCursorRay();
        cursorPrevOrigin = cursorOrigin;
        cursorPrevDir = cursorDir;
    }

    private void BuildScanLevels(int n)
    {
        scanLevels[0] = cellStartBuffer;
        scanCounts[0] = n;
        scanLevelCount = 1;

        while (true)
        {
            int blocks = (scanCounts[scanLevelCount - 1] + ScanBlock - 1) / ScanBlock;
            scanLevels[scanLevelCount] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, blocks, sizeof(uint));
            scanCounts[scanLevelCount] = blocks;
            if (blocks <= 1 || scanLevelCount == MaxScanLevels) break;
            scanLevelCount++;
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, maxDeltaTime);
        simTime += dt;
        frame++;

        Vector3 target = new(
            Mathf.Sin(simTime * targetSpeed) * targetRadius.x,
            Mathf.Sin(simTime * targetSpeed * 1.7f + 1f) * targetRadius.y,
            Mathf.Cos(simTime * targetSpeed * 1.3f) * targetRadius.z);

        UpdateCursor();

        compute.SetFloat(DtId, dt);
        compute.SetFloat(TimeId, simTime);
        compute.SetInt(FrameId, frame);
        compute.SetVector(TargetId, target);
        compute.SetVector(CursorOriginId, cursorOrigin);
        compute.SetVector(CursorDirId, cursorDir);
        compute.SetVector(CursorPrevOriginId, cursorPrevOrigin);
        compute.SetVector(CursorPrevDirId, cursorPrevDir);
        compute.SetVector(CursorParamsId, new Vector4(cursorRadius, cursorSlope, cursorStrength * cursorFactor, cursorTrail));
        compute.SetVector(ShockCenterId, shockCenter);
        float shockFade = shockMotion.IsActive() ? 1f - shockRadius / Mathf.Max(shockMaxRadius, 1e-3f) : 0f;
        compute.SetVector(ShockParamsId, new Vector4(shockRadius, shockWidth, shockStrength * shockFade, 0f));
        compute.SetVector(WindVelocityId, WindVelocity() * windDrift);

        compute.Dispatch(kernelClear, cellGroups, 1, 1);
        compute.Dispatch(kernelCount, boidGroups, 1, 1);
        DispatchScan();
        compute.Dispatch(kernelScatter, boidGroups, 1, 1);
        compute.Dispatch(kernelStep, boidGroups, 1, 1);

        cursorPrevOrigin = cursorOrigin;
        cursorPrevDir = cursorDir;

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

    private void DispatchScan()
    {
        for (int l = 0; l < scanLevelCount; l++)
        {
            compute.SetBuffer(kernelScan, ScanDataId, scanLevels[l]);
            compute.SetBuffer(kernelScan, ScanSumsId, scanLevels[l + 1]);
            compute.SetInt(ScanCountId, scanCounts[l]);
            compute.Dispatch(kernelScan, scanCounts[l + 1], 1, 1);
        }

        for (int l = scanLevelCount - 2; l >= 0; l--)
        {
            compute.SetBuffer(kernelAdd, ScanDataId, scanLevels[l]);
            compute.SetBuffer(kernelAdd, ScanSumsId, scanLevels[l + 1]);
            compute.SetInt(ScanCountId, scanCounts[l]);
            compute.Dispatch(kernelAdd, scanCounts[l + 1], 1, 1);
        }
    }

    private Vector3 WindVelocity()
    {
        if (windField != null)
        {
            WindParams w = windField.GetParams(simTime);
            float s = w.strength * (1f + w.pulseMagnitude * Mathf.Sin(simTime * w.pulse));
            return new Vector3(w.dir.x * s, 0f, w.dir.y * s);
        }
        Vector2 d = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.zero;
        return new Vector3(d.x, 0f, d.y) * windStrength;
    }

    private void UpdateCursorRay()
    {
        if (cam == null || Mouse.current == null) return;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        cursorOrigin = ray.origin - transform.position;
        cursorDir = ray.direction;
    }

    private void UpdateCursor()
    {
        if (cam == null) cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null)
        {
            SetCursorGoal(0f);
            return;
        }

        Vector2 pos = mouse.position.ReadValue();
        bool inside = pos.x >= 0f && pos.y >= 0f && pos.x <= Screen.width && pos.y <= Screen.height;
        float now = Time.unscaledTime;

        if ((pos - lastMouse).sqrMagnitude > cursorMoveThreshold * cursorMoveThreshold)
        {
            lastMouse = pos;
            lastMoveTime = now;
        }

        UpdateCursorRay();

        bool active = inside && Application.isFocused && now - lastMoveTime < cursorIdleTime;
        SetCursorGoal(active ? 1f : 0f);

        if (inside && Application.isFocused && mouse.leftButton.wasPressedThisFrame) StartShock();
    }

    private void SetCursorGoal(float goal)
    {
        if (Mathf.Approximately(cursorGoal, goal)) return;
        cursorGoal = goal;
        cursorMotion.TryCancel();

        float duration = goal > cursorFactor ? cursorFadeIn : cursorFadeOut;
        cursorMotion = LMotion.Create(cursorFactor, goal, Mathf.Max(duration * Mathf.Abs(goal - cursorFactor), 1e-3f))
            .WithEase(cursorEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(this, static (v, self) => self.cursorFactor = v);
    }

    private void StartShock()
    {
        float t = Mathf.Max(Vector3.Dot(-cursorOrigin, cursorDir), 0f);
        shockCenter = cursorOrigin + cursorDir * t;
        shockMotion.TryCancel();
        shockRadius = 0f;
        shockMotion = LMotion.Create(0f, shockMaxRadius, Mathf.Max(shockDuration, 1e-3f))
            .WithEase(shockEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(this, static (v, self) => self.shockRadius = v);
    }

    private void OnDisable()
    {
        cursorMotion.TryCancel();
        shockMotion.TryCancel();
        cursorFactor = 0f;
        cursorGoal = 0f;
        shockRadius = 0f;

        boids[0]?.Release();
        boids[1]?.Release();
        cellStartBuffer?.Release();
        boidKeyBuffer?.Release();
        argsBuffer?.Release();
        for (int i = 1; i < scanLevels.Length; i++)
        {
            scanLevels[i]?.Release();
            scanLevels[i] = null;
        }
        scanLevels[0] = null;
        boids[0] = null;
        boids[1] = null;
        cellStartBuffer = null;
        boidKeyBuffer = null;
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