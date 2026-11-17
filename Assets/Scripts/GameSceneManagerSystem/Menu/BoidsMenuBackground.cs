using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

using TheRavine.Generator;

public sealed class BoidsMenuBackground : MonoBehaviour
{
    private static readonly int CursorOriginId = Shader.PropertyToID("cursorOrigin");
    private static readonly int CursorDirId = Shader.PropertyToID("cursorDir");
    private static readonly int CursorPrevOriginId = Shader.PropertyToID("cursorPrevOrigin");
    private static readonly int CursorPrevDirId = Shader.PropertyToID("cursorPrevDir");
    private static readonly int CursorParamsId = Shader.PropertyToID("cursorParams");
    private static readonly int ShockCenterId = Shader.PropertyToID("shockCenter");
    private static readonly int ShockParamsId = Shader.PropertyToID("shockParams");
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

    private BoidFlockGpu flock;
    private GraphicsBuffer argsBuffer;
    private MaterialPropertyBlock props;
    private Mesh mesh;
    private Camera cam;
    private WindField windField;
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
        flock = new BoidFlockGpu(compute, count, extents, perception);

        mesh = BoidFlockGpu.BuildMesh();
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

        BoidFlockGpu.ApplyWeights(compute, new BoidFlockWeights
        {
            separationRadius = separationRadius,
            separationMin = separationMin,
            maxNeighbors = maxNeighbors,
            separation = separationWeight,
            alignment = alignmentWeight,
            cohesion = cohesionWeight,
            bounds = boundsWeight,
            target = targetWeight,
            noise = noiseWeight,
            speedRange = speedRange,
            maxSteer = maxSteer
        });

        props = new MaterialPropertyBlock();
        props.SetBuffer(RenderBoidsId, flock.Output);

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

        BoidFlockGpu.ApplyFrame(compute, dt, simTime, frame, target, WindVelocity() * windDrift);
        compute.SetVector(CursorOriginId, cursorOrigin);
        compute.SetVector(CursorDirId, cursorDir);
        compute.SetVector(CursorPrevOriginId, cursorPrevOrigin);
        compute.SetVector(CursorPrevDirId, cursorPrevDir);
        compute.SetVector(CursorParamsId, new Vector4(cursorRadius, cursorSlope, cursorStrength * cursorFactor, cursorTrail));
        compute.SetVector(ShockCenterId, shockCenter);
        float shockFade = shockMotion.IsActive() ? 1f - shockRadius / Mathf.Max(shockMaxRadius, 1e-3f) : 0f;
        compute.SetVector(ShockParamsId, new Vector4(shockRadius, shockWidth, shockStrength * shockFade, 0f));

        flock.Dispatch();

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

        flock?.Dispose();
        flock = null;
        argsBuffer?.Release();
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
        flock.SetData(data);
    }
}