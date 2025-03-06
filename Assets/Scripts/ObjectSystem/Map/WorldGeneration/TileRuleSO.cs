using UnityEngine;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "TileRule", menuName = "WFC/Tile Rule")]
    public class TileRuleSO : ScriptableObject
    {
        public GameObject prefab;
        public TileRule[] rules;
        public Vector2Int[] connectionPoints;
        
        [Range(1, 100)]
        public int weight = 10;
    }
}