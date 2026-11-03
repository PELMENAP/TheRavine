using System;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class Footprint
    {
        public static int Resolve(ObjectInfo info, Vector2Int origin, Span<Vector2Int> cells)
        {
            if (info == null || info.AdditionalOccupiedCells == null) return 0;
            return Resolve(info.AdditionalOccupiedCells, origin, cells);
        }

        public static int Resolve(ReadOnlySpan<Vector2Int> offsets, Vector2Int origin, Span<Vector2Int> cells)
        {
            int count = offsets.Length;
            if (count > cells.Length) return count;

            for (int i = 0; i < count; i++)
                cells[i] = origin + offsets[i] * MapGenerator.scale;

            return count;
        }
    }
}
