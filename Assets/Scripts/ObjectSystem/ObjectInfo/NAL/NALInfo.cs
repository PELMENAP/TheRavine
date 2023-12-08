using UnityEngine;
[CreateAssetMenu(fileName = "NALInfo", menuName = "Gameplay/Items/Create New NALInfo")]
public class NAlInfo : ScriptableObject, INALInfo
{
    [SerializeField] private byte _maxcount;
    [SerializeField] private byte _around;
    [SerializeField] private byte _distance;
    [SerializeField] private byte _chance;
    [SerializeField] private byte _delay;
    public byte maxcount => _maxcount;
    public byte around => _around;
    public byte distance => _distance;
    public byte chance => _chance;
    public byte delay => _delay;
}
