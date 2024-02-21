using UnityEngine;
[CreateAssetMenu(fileName = "EntityMovementStatsInfo", menuName = "Gameplay/Create New EntityMovementStatsInfo")]
public class EntityMovementStatsInfo : ScriptableObject
{
    [Min(0)]
    public int BaseSpeed;
}
