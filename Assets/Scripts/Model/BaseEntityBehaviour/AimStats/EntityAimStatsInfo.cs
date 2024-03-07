using UnityEngine;
[CreateAssetMenu(fileName = "EntityAimStatsInfo", menuName = "Gameplay/Create New EntityAimStatsInfo")]
public class EntityAimStatsInfo : ScriptableObject
{
    [Min(0)]
    public int CrosshairDistanse;
    [Min(0)]
    public int MaxCrosshairDistanse;
}
