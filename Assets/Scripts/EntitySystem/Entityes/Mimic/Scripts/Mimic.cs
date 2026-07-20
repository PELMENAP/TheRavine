using UnityEngine;
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
    private IVelocitySource velocitySource;

    public Vector3 Velocity { get; private set; }
    public Vector3 legPlacerOrigin;

    private Vector3 lastForward = Vector3.forward;

    private void Awake()
    {
        velocitySource = velocitySourceBehaviour as IVelocitySource ?? GetComponent<IVelocitySource>();

        maxLegs = numberOfLegs * partsPerLeg;
        minimumAnchoredParts = minimumAnchoredLegs * partsPerLeg;
        maxLegDistance = newLegRadius * 2.1f;

        Pool = new LegPool(maxLegs, legResolution);
        planner = new LegPlanner();
        animator = new LegAnimator();
        legRenderer = new LegRenderer();
        legRenderer.Initialize(this);
    }


    private async void Start()
    {
        MapGenerator = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
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
        legRenderer.UpdateMesh(this);
    }
}