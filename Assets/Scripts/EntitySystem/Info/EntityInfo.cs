using UnityEngine;
[CreateAssetMenu(fileName = "EntityInfo", menuName = "Gameplay/Create New EntityInfo")]
public class EntityInfo : ScriptableObject
{
    [SerializeField] private string _Name;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private EnergyInfo _energyInfo;
    [SerializeField] private EntityMovementInfo _movementInfo;
    [SerializeField] private EntityAimStatsInfo _aimStatsInfo;
    [SerializeField] private BindInfo _bindInfo;
    public string Name => _Name;
    public GameObject prefab => _prefab;
    public EnergyInfo energyInfo => _energyInfo;
    public EntityMovementInfo movementInfo => _movementInfo;
    public EntityAimStatsInfo aimStatsInfo => _aimStatsInfo;
    public BindInfo bindInfo => _bindInfo;
}
