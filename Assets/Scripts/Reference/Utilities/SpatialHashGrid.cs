using UnityEngine;
using System.Collections.Generic;

namespace TheRavine.Extensions
{
    public class SpatialHashGrid<T>
    {
        private Dictionary<int, List<T>> _grid = new();
        private float _cellSize;
        
        public SpatialHashGrid(float cellSize)
        {
            _cellSize = cellSize;
        }
        
        private int GetHashCode(Vector2Int position)
        {
            int x = Mathf.FloorToInt(position.x / _cellSize);
            int y = Mathf.FloorToInt(position.y / _cellSize);
            return x * 73856093 ^ y * 19349663;
        }
        
        public void Add(Vector2Int position, T item)
        {
            int hash = GetHashCode(position);
            
            if (!_grid.TryGetValue(hash, out var list))
            {
                list = new List<T>();
                _grid[hash] = list;
            }
            
            list.Add(item);
        }
        
        public List<T> GetItemsAt(Vector2Int position)
        {
            int hash = GetHashCode(position);
            
            if (_grid.TryGetValue(hash, out var list))
            {
                return list;
            }
            
            return new List<T>();
        }
        
        public void Clear()
        {
            _grid.Clear();
        }
    }
}