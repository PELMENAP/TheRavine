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
        public int priorityWeight = 1;
        
        private HashSet<TileRuleSO> _allowedNeighborsSet;
        
        public bool IsNeighborAllowed(TileRuleSO neighbor)
        {
            if (_allowedNeighborsSet == null)
                _allowedNeighborsSet = new HashSet<TileRuleSO>(allowedNeighbors);
            return _allowedNeighborsSet.Contains(neighbor);
        }
        public static Direction GetOppositeDirection(Direction dir)
        {
            return dir switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => dir
            };
        }
    }
}