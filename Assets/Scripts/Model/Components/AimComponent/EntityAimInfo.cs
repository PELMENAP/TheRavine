using UnityEngine;

[CreateAssetMenu(fileName = "EntityAimInfo", menuName = "Gameplay/Create New EntityAimInfo")]
public class EntityAimInfo : ScriptableObject
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
