using UnityEngine;

[CreateAssetMenu(fileName = "EntityStatsInfo", menuName = "Gameplay/Create New EntityStatsInfo")]
public class EntityStatsInfo : ScriptableObject
{
    [Min(0)]
    public int Energy;
    [Min(0)]
    public int MaxEnergy;
}
