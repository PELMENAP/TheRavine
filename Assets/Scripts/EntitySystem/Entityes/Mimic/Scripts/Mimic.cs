using UnityEngine;
using Unity.Mathematics;
using TheRavine.Generator;

[RequireComponent(typeof(IVelocitySource))]
public class Mimic : MonoBehaviour
{
    public Material legMaterial;
    public int numberOfLegs = 5;
    public int partsPerLeg = 4;
    public int minimumAnchoredLegs = 2;
    public float minLegLifetime = 5f;
    public float maxLegLifetime = 15f;
    public float newLegRadius = 3f;
    public float minLegDistance = 4.5f;
    public float maxLegDistance = 6.3f;
    public int legResolution = 40;
    public int verticeCount = 6;
    public float minGrowCoef = 4.5f;
    public float maxGrowCoef = 6.5f;
    public float newLegCooldown = 0.3f;

    public float legMinHeight = 1f;
    public float legMaxHeight = 3f;
    public float handleOffsetMinRadius = 0.5f;
    public float handleOffsetMaxRadius = 1.5f;
    public float finalFootDistance = 1f;
    public float minRotSpeed = 10f;
    public float maxRotSpeed = 30f;
    public float minOscillationSpeed = 1f;
    public float maxOscillationSpeed = 3f;
    public float legWidth = 0.2f;
    public int legCount;
    public int deployedLegs;
    public int minimumAnchoredParts;
    public int maxLegs;

    public LegPool Pool { get; private set; }
    public MapGenerator MapGenerator { get; private set; }

    private LegPlanner planner;
    private LegAnimator animator;
    private LegRenderer legRenderer;

    [SerializeField] private MonoBehaviour velocitySourceBehaviour;

    [Header("Mesh")]
    [SerializeField, Range(0.1f, 1f)] private float meshQuality = 1f;

    public float MeshQuality => meshQuality;

    [Header("LOD")]
    [SerializeField] private float lodNearDistance = 40f;
    [SerializeField] private float lodFarDistance = 90f;
    [SerializeField] private float lodCullDistance = 200f;
    [SerializeField, Range(0.1f, 1f)] private float lodMidQuality = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float lodFarQuality = 0.3f;

    private const int   CullLevel   = 3;
    private const float MaxAccumDt  = 0.25f;
    private const float LodHysteresis = 0.9f;

    private static readonly int[] LodInterval = { 1, 2, 3, 8 };

    private static int    _nextId;
    private static Camera _lodCamera;
    private static int    _lodCameraFrame = -1;
    private static bool   _lodCameraValid;
    private static Vector3 _lodCameraPosition;

    private int   _id;
    private int   _lod;
    private float _accumDt;
    private IVelocitySource velocitySource;

    public Vector3 Velocity { get; private set; }
    public Vector3 legPlacerOrigin;

    private Vector3 lastForward = Vector3.forward;

    private bool _phenotypeApplied;
    private bool _structuresReady;

    private void Awake()
    {
        _id = _nextId++;
        velocitySource = velocitySourceBehaviour as IVelocitySource ?? GetComponent<IVelocitySource>();
    }

    public void ApplyGeneticPhenotype(GeneticParameters genetics)
    {
        MimicPhenotype.FromGenetics(genetics).ApplyTo(this);
        _phenotypeApplied = true;
    }

    private async void Start()
    {
        if (!_phenotypeApplied)
            ApplyGeneticPhenotype(GeneticParameters.Default);

        maxLegs = numberOfLegs * partsPerLeg;
        minimumAnchoredParts = minimumAnchoredLegs * partsPerLeg;
        maxLegDistance = newLegRadius * 2.1f;

        MapGenerator = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
    }

    private void InitializeStructures()
    {
        _structuresReady = true;

        Pool = new LegPool(maxLegs);
        planner = new LegPlanner();
        animator = new LegAnimator();
        legRenderer = new LegRenderer();
        legRenderer.Initialize(this);
    }

    private void OnDestroy()
    {
        legRenderer?.Dispose();
    }

    private void OnEnable() => legRenderer?.ApplyShown();

    private static bool TryGetCameraPosition(out Vector3 position)
    {
        int frame = Time.frameCount;
        if (frame != _lodCameraFrame)
        {
            _lodCameraFrame = frame;
            if (_lodCamera == null) _lodCamera = Camera.main;
            _lodCameraValid = _lodCamera != null;
            if (_lodCameraValid) _lodCameraPosition = _lodCamera.transform.position;
        }
        position = _lodCameraPosition;
        return _lodCameraValid;
    }

    private float LodThreshold(int level) => level == 0 ? lodNearDistance : level == 1 ? lodFarDistance : lodCullDistance;

    private void UpdateLod()
    {
        int level = 0;
        if (TryGetCameraPosition(out Vector3 cameraPosition))
        {
            float distance = math.distance((float3)cameraPosition, (float3)transform.position);
            level = _lod;
            while (level < CullLevel && distance >= LodThreshold(level)) level++;
            while (level > 0 && distance < LodThreshold(level - 1) * LodHysteresis) level--;
        }

        if (level == _lod) return;
        _lod = level;

        legRenderer.SetShown(level < CullLevel);
        legRenderer.SetDetail(level == 0 ? 1f : level == 1 ? lodMidQuality : lodFarQuality);
    }

    private void Update()
    {
        if (!_structuresReady) InitializeStructures();
        if (MapGenerator == null || velocitySource == null) return;

        _accumDt += Time.deltaTime;
        if ((Time.frameCount + _id) % LodInterval[_lod] != 0) return;

        float dt = math.min(_accumDt, MaxAccumDt);
        _accumDt = 0f;

        UpdateLod();

        Velocity = velocitySource.Velocity;
        if (Velocity.sqrMagnitude > 0.01f)
            lastForward = Velocity.normalized;

        legPlacerOrigin = transform.position + lastForward * newLegRadius;

        planner.Update(this, dt);
        animator.Update(this, dt);
        if (_lod < CullLevel) legRenderer.UpdateMesh(this);
    }
}
