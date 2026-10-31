using UnityEngine;
using Unity.Mathematics;
using TheRavine.Extensions;
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

    [Header("Leg LOD")]
    [SerializeField] private int lodMidRing = 1;
    [SerializeField] private int lodFarRing = 2;
    [SerializeField] private int lodRecheckFrames = 15;
    [SerializeField] private int lodMidStride = 2;
    [SerializeField] private int lodFarStride = 4;
    [SerializeField] private int lodMidVertices = 4;
    [SerializeField] private int lodFarVertices = 3;

    private long lodChunk = long.MinValue;
    private int lodFrames;

    public int Lod { get; private set; }
    public int LodStride => Lod == 0 ? 1 : Lod == 1 ? lodMidStride : lodFarStride;
    public int LodRingVertices => Lod == 0 ? verticeCount : Lod == 1 ? lodMidVertices : lodFarVertices;
    public bool LodNormalsPerFrame => Lod < 2;
    private IVelocitySource velocitySource;

    public Vector3 Velocity { get; private set; }
    public Vector3 legPlacerOrigin;

    private Vector3 lastForward = Vector3.forward;

    private bool _phenotypeApplied;

    private void Awake()
    {
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

        InitializeStructures();

        MapGenerator = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
    }

    private void InitializeStructures()
    {
        maxLegs = numberOfLegs * partsPerLeg;
        minimumAnchoredParts = minimumAnchoredLegs * partsPerLeg;
        maxLegDistance = newLegRadius * 2.1f;

        Pool = new LegPool(maxLegs, legResolution);
        planner = new LegPlanner();
        animator = new LegAnimator();
        legRenderer = new LegRenderer();
        legRenderer.Initialize(this);
    }
    private void Update()
    {
        if (MapGenerator == null || velocitySource == null) return;

        Velocity = velocitySource.Velocity;
        if (Velocity.sqrMagnitude > 0.01f)
            lastForward = Velocity.normalized;

        legPlacerOrigin = transform.position + lastForward * newLegRadius;

        planner.Update(this, Time.deltaTime);
        animator.Update(this, Time.deltaTime);
        RefreshLod();
        legRenderer.UpdateMesh(this);
    }

    private void RefreshLod()
    {
        Vector3 p = transform.position;
        long chunk = MapGenerator.WorldToChunk(Position2Int.Pack((int)math.floor(p.x), (int)math.floor(p.z)));
        if (chunk == lodChunk && ++lodFrames < lodRecheckFrames) return;

        lodChunk = chunk;
        lodFrames = 0;

        int ring = MapGenerator.RingOfChunk(chunk);
        Lod = ring < 0 || ring >= lodFarRing ? 2 : ring >= lodMidRing ? 1 : 0;
    }
}