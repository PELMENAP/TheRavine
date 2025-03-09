using UnityEngine;
using System;
using System.Collections.Generic;

namespace TheRavine.Generator
{
    [Serializable]
    public class TileRule
    {
        public Direction direction;
        public TileRuleSO[] allowedNeighbors;
    }
}