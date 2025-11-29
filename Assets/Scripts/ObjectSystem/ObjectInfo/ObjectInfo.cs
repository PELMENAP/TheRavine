using UnityEngine;

[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject
{
    [SerializeField] private ushort defaultAmount;
    [SerializeField] private ushort initialPoolSize;
    [SerializeField] private InstanceType instanceType;
    [SerializeField] private BehaviourType behaviourType;
    [SerializeField] private NAlInfo nalInfo;
    [SerializeField] private InventoryItemInfo inventoryItemInfo;
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private ObjectInfo evolutionStep;
    [SerializeField] private Vector2Int[] additionalOccupiedCells;
    [SerializeField] private SpreadPattern onDeathPattern;
    [SerializeField] private SpreadPattern onPickUpPattern;

    private int? cachedPrefabID;

    public string ObjectName;
    public int PrefabID => cachedPrefabID ??= objectPrefab.GetHashCode();
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