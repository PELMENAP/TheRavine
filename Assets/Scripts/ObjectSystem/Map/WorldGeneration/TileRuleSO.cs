using UnityEngine;
using System;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "TileRule", menuName = "WFC/Tile Rule")]
    public class TileRuleSO : ScriptableObject
    {
        public GameObject prefab;
        public TileRule[] rules;
        
        [Range(1, 100), Tooltip("Чем больше вес, тем больше шанс генерации данного тайла.")]
        public int weight = 10;
    }

    [Serializable]
    public class TileRule
    {
        public Direction direction;
        public TileRuleSO[] allowedNeighbors;
    }
}