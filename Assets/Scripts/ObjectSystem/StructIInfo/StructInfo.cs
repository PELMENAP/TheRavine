using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New StructInfo")]
public class StructInfo : ScriptableObject
{
    public byte distortion;
    public TileInfo[] tileInfo;
}

[System.Serializable]
public struct TileInfo
{
    public byte[] neight;
    public byte MCount;
    public ObjectInfo objectInfo;
}