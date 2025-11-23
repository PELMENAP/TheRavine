using UnityEngine;
[CreateAssetMenu(fileName = "EntityInfo", menuName = "Gameplay/Create New EntityInfo")]
public class EntityInfo : ScriptableObject
{
    [SerializeField] private string _Name;
    [SerializeField] private GameObject _prefab;
    [SerializeField] private EnergyInfo _energyInfo;
    [SerializeField] private EntityMovementInfo _movementInfo;
    [SerializeField] private EntityAimInfo _aimStatsInfo;
    [SerializeField] private BindInfo _bindInfo;
    public string Name => _Name;
    public GameObject Prefab => _prefab;
    public EnergyInfo EnergyInfo => _energyInfo;
    public EntityMovementInfo MovementInfo => _movementInfo;
    public EntityAimInfo AimStatsInfo => _aimStatsInfo;
    public BindInfo BindInfo => _bindInfo;
}
