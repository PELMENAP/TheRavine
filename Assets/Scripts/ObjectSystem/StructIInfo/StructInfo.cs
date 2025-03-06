using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New StructInfo")]
public class StructInfo : ScriptableObject
{
    public byte distortion;
    public TileInfo[] tileInfo; // tile in each 9 sides
}

[System.Serializable]
public struct TileInfo
{
    public byte[] countBySide; // count of tile in each 8 sides
    public byte MCount; //max count
    public ObjectInfo objectInfo; //prefab
}