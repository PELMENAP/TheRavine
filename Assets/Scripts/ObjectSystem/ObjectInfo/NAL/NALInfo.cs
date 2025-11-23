using UnityEngine;
[CreateAssetMenu(fileName = "NALInfo", menuName = "Gameplay/Items/Create New NALInfo")]
public class NAlInfo : ScriptableObject, INALInfo
{
    [SerializeField] private readonly byte _distance;
    [SerializeField] private readonly byte _chance;
    [SerializeField] private readonly byte _attempt;
    [SerializeField] private readonly byte _delay;
    public byte distance => _distance;
    public byte chance => _chance;
    public byte attempt => _attempt;
    public byte delay => _delay;
}
