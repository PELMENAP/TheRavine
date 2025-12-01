using UnityEngine;

[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject
{
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

    private int? cachedPrefabID;

    public string ObjectName;
    public int PrefabID => cachedPrefabID ??= objectPrefab.GetInstanceID();
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

    private void OnValidate()
    {
        cachedPrefabID = null;
        
        if (string.IsNullOrEmpty(ObjectName) && objectPrefab != null)
            ObjectName = objectPrefab.name;
    }
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