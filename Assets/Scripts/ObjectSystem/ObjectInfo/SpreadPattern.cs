using UnityEngine;
[CreateAssetMenu(fileName = "SpreadPattern", menuName = "Gameplay/Create New SpreadPattern")]
public class SpreadPattern : ScriptableObject
{
    public ObjectInfo main;
    public ObjectInfo[] other;
    public byte factor;
}