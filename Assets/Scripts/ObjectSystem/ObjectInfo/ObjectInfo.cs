using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject, IObjectInfo
{
    [SerializeField] private ushort _amount;
    [SerializeField] private ushort _poolSize;
    [SerializeField] private InstanceType _iType;
    [SerializeField] private BehaviourType _bType;
    [SerializeField] private NAlInfo _nalinfo;
    [SerializeField] private InventoryItemInfo _iteminfo;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private ObjectInfo _nextStep;
    [SerializeField] private Vector2[] _addspace;
    [SerializeField] private SpreadPattern _deadPattern, _pickUpPattern;
    public ushort amount => _amount;
    public ushort poolSize => _poolSize;
    public InstanceType iType => _iType;
    public BehaviourType bType => _bType;
    public NAlInfo nalinfo => _nalinfo;
    public InventoryItemInfo iteminfo => _iteminfo;
    public GameObject prefab => _prefab;
    public ObjectInfo nextStep => _nextStep;
    public Vector2[] addspace => _addspace;
    public SpreadPattern deadPattern => _deadPattern;
    public SpreadPattern pickUpPattern => _pickUpPattern;
}
