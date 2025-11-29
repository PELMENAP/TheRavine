using UnityEngine;
[CreateAssetMenu(fileName = "NALInfo", menuName = "Gameplay/Items/Create New NALInfo")]
public class NAlInfo : ScriptableObject, INALInfo
{
    [SerializeField] private byte _distance;
    [SerializeField] private byte _chance;
    [SerializeField] private byte _attempt;
    [SerializeField] private byte _delay;
    public byte distance => _distance;
    public byte chance => _chance;
    public byte attempt => _attempt;
    public byte delay => _delay;
}
