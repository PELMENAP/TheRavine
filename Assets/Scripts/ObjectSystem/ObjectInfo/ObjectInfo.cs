using UnityEngine;

[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject
{
    public const int MaxVisibleRings = 4;

    [SerializeField] private ushort id;
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private ushort defaultAmount;
    [Min(1)]
    [SerializeField] private ushort initialPoolSize = 10;
    [SerializeField] private InstanceType instanceType;
    [SerializeField] private BehaviourType behaviourType;

    [ConditionalField(nameof(behaviourType), BehaviourType.NAL)]
    [SerializeField] private NAlInfo nalInfo;

    [ConditionalField(nameof(instanceType), InstanceType.Static, inverse: true)]
    [SerializeField] private InventoryItemInfo inventoryItemInfo;


    [ConditionalField(nameof(behaviourType), BehaviourType.GROW)]
    [SerializeField] private ObjectInfo evolutionStep;

    [SerializeField] private Vector2Int[] additionalOccupiedCells;
    [ConditionalField(nameof(behaviourType), BehaviourType.NAL)]
    [SerializeField] private SpreadPattern onDeathPattern;

    [ConditionalField(nameof(instanceType), InstanceType.Interactable)]
    [SerializeField] private SpreadPattern onPickUpPattern;

    [Range(0, MaxVisibleRings)]
    [SerializeField] private int visibleRings;
    [SerializeField] private FarMode farMode;
    [SerializeField] private bool castShadowsFar;
    [SerializeField] private ObjectLodSet lodSet;

    public ushort Id => id;
    public ushort DefaultAmount => defaultAmount;
    public ushort InitialPoolSize => initialPoolSize;
    public InstanceType InstanceType => instanceType;
    public BehaviourType BehaviourType => behaviourType;
    public NAlInfo NalInfo => nalInfo;
    public InventoryItemInfo InventoryItemInfo => inventoryItemInfo;
    public GameObject ObjectPrefab => objectPrefab;
    public ObjectInfo EvolutionStep => evolutionStep;
    public Vector2Int[] AdditionalOccupiedCells => additionalOccupiedCells;
    public SpreadPattern OnDeathPattern => onDeathPattern;
    public SpreadPattern OnPickUpPattern => onPickUpPattern;
    public byte VisibleRings => (byte)Mathf.Clamp(visibleRings, 0, MaxVisibleRings);
    public FarMode FarMode => farMode;
    public bool CastShadowsFar => castShadowsFar;
    public ObjectLodSet LodSet => lodSet;
    public bool HasLifecycle => behaviourType == BehaviourType.NAL || behaviourType == BehaviourType.GROW;

#if UNITY_EDITOR
    public void EditorAssignId(ushort value) => id = value;
#endif
}

public enum BehaviourType : byte
{
    None = 0,
    NAL = 1,
    GROW = 2
}

public enum InstanceType : byte
{
    Static = 0,
    Interactable = 1
}

public enum FarMode : byte
{
    Hidden = 0,
    Mesh = 1,
    MeshThenBillboard = 2
}
