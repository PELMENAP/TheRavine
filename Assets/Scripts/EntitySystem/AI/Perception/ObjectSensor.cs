using Unity.Mathematics;

using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public static class ObjectSensor
{
    public static bool TryFindNearest(IWorldCommandTarget world, ObjectInfoRegistry registry, in float2 self, int radius,
        out float2 position, out float distance)
    {
        position = default;
        distance = -1f;
        if (world == null || registry == null || radius <= 0) return false;

        radius = math.min(radius, SimulationRules.MaxObjectSenseRadius);
        Position2Int.Unpack(ChunkFoodIndex.CellOf(self.x, self.y), out int cx, out int cy);

        float best  = float.MaxValue;
        bool  found = false;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            long cell = Position2Int.Pack(cx + dx, cy + dy);
            if (!world.TryGetObject(cell, out ObjectInstInfo inst)) continue;

            float2 center = ChunkFoodIndex.CellCenter(cell);
            float d2 = math.distancesq(center, self);
            if (d2 >= best) continue;

            ObjectInfo info = registry.Get(inst.PrefabID);
            if (info == null || info.InventoryItemInfo == null || info.IsFood) continue;

            best     = d2;
            position = center;
            found    = true;
        }

        if (found) distance = math.sqrt(best);
        return found;
    }
}
