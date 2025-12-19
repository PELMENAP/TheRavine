using UnityEngine;
using System.Collections.Generic;
using System;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "TilePattern", menuName = "WFC/Tile Pattern")]
    public class TilePatternSO : ScriptableObject
    {
        [Tooltip("Шаблоны для начальной генерации.")]
        public List<TilePattern> initialPatterns;
    }

    [Serializable]
    public class TilePattern
    {
        public Vector2Int startOffset;
        public TileRuleSO tile;
    }
}