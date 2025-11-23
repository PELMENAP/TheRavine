using UnityEngine;

[CreateAssetMenu(fileName = "EntityStatsInfo", menuName = "Gameplay/Create New EnergyInfo")]
public class EnergyInfo : ScriptableObject
{
    [Min(0)]
    public int Energy;
    [Min(0)]
    public int MaxEnergy;
}
