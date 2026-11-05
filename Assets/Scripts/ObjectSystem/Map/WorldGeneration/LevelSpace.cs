using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class LevelSpace
    {
        public static Vector3 Resolve(Vector3 terrainPosition, PlacementKind kind, int baseLevel, float levelHeight)
        {
            if (kind == PlacementKind.Construction3D && levelHeight > 0f)
                terrainPosition.y = (math.floor(terrainPosition.y / levelHeight) + baseLevel) * levelHeight;
            else
                terrainPosition.y += baseLevel * levelHeight;
            return terrainPosition;
        }
    }
}
