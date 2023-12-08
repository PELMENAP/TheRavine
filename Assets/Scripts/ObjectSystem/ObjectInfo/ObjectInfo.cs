using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Items/Create New ObjectInfo")]
public class ObjectInfo : ScriptableObject, IObjectInfo
{
    [SerializeField] private string _name;
    [SerializeField] private ushort _amount;
    [SerializeField] private ushort _poolSize;
    [SerializeField] private InstanceType _iType;
    [SerializeField] private BehaviourType _bType;
    [SerializeField] private NAlInfo _nalinfo;
    [SerializeField] private GameObject _prefab;
    public string title => _name;
    public ushort amount => _amount;
    public ushort poolSize => _poolSize;
    public InstanceType iType => _iType;
    public BehaviourType bType => _bType;
    public NAlInfo nalinfo => _nalinfo;
    public GameObject prefab => _prefab;
}
