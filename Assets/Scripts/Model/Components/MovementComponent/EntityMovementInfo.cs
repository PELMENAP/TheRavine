using UnityEngine;
[CreateAssetMenu(fileName = "EntityMovementStatsInfo", menuName = "Gameplay/Create New EntityMovementStatsInfo")]
public class EntityMovementInfo : ScriptableObject
{
    [Min(0)]
    public int BaseSpeed;
    [Min(0)]
    public int Acceleration;
    [Min(0)] 
    public int Deceleration;
}
