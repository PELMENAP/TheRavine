using UnityEngine;
[CreateAssetMenu(fileName = "ObjectInfo", menuName = "Gameplay/Create New StructInfo")]
public class StructInfo : ScriptableObject
{
    public byte distortion, iteration;
    public TileInfo[] tileInfo;
}

[System.Serializable]
public struct TileInfo
{
    public byte[] neight;
    public byte MCount;
    public GameObject prefab;
}