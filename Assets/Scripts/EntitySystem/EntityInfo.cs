using UnityEngine;
[CreateAssetMenu(fileName = "EntityInfo", menuName = "Gameplay/Create New EntityInfo")]
public class EntityInfo : ScriptableObject
{
    [SerializeField] private string _Name;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private EntityStatsInfo _statsInfo;
    [SerializeField] private ushort _poolSize;
    public string Name => _Name;
    public GameObject prefab => _prefab;
    public EntityStatsInfo statsInfo => _statsInfo;
    public ushort poolSize => _poolSize;
}
