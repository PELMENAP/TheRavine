using UnityEngine;
[CreateAssetMenu(fileName = "EntityAimStatsInfo", menuName = "Gameplay/Create New EntityAimStatsInfo")]
public class EntityAimStatsInfo : ScriptableObject
{
    [Min(0)]
    public int CrosshairDistance;
    [Min(0)]
    public int MaxCrosshairDistance;
    [Min(0)]
    public int CrosshairOffset;
    [Min(0)]
    public int PickDistance;
}
