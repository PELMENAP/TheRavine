using Unity.Mathematics;

using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public static class ObjectSensor
{
    public static bool TryFindNearest(IWorldCommandTarget world, ObjectInfoRegistry registry, in float2 self, int radius,
        out float2 position, out float distance, out int prefabId)
    {
        position = default;
        distance = -1f;
        prefabId = ObjectInstInfo.PlaceholderId;
        if (world == null || registry == null || radius <= 0) return false;

        radius = math.min(radius, SimulationRules.MaxObjectSenseRadius);
        Position2Int.Unpack(ChunkFoodIndex.CellOf(self.x, self.y), out int cx, out int cy);

        float best  = float.MaxValue;
        bool  found = false;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            long cell = Position2Int.Pack(cx + dx, cy + dy);
            if (!world.TryGetObject(cell, out ObjectInstInfo inst) || !IsFuel(registry.Get(inst.PrefabID), in inst)) continue;

            float2 center = ChunkFoodIndex.CellCenter(cell);
            float d2 = math.distancesq(center, self);
            if (d2 >= best) continue;

            best     = d2;
            position = center;
            prefabId = inst.PrefabID;
            found    = true;
        }

        if (found) distance = math.sqrt(best);
        return found;
    }

    public static bool IsFuel(ObjectInfo info, in ObjectInstInfo item)
        => info != null && info.ObjectCanBurn && !info.IsFood && !info.HasLifecycle
        && info.InstanceType == InstanceType.Interactable && info.InventoryItemInfo != null
        && info.InventoryItemInfo.isPlaceable && info.PlacementKind == PlacementKind.Surface2D
        && item.Amount > 0 && item.Levels == 1 && item.SecondaryCount == 0
        && (info.AdditionalOccupiedCells == null || info.AdditionalOccupiedCells.Length == 0)
        && info.OnPickUpPattern == null;

    public static bool TryTakeFuel(IWorldCommandTarget world, ObjectInfoRegistry registry, long cell, int expectedId, out ObjectInstInfo item)
    {
        item = default;
        return world.TryGetObject(cell, out item) && item.PrefabID == expectedId
            && IsFuel(registry.Get(item.PrefabID), in item) && world.RemoveObject(cell);
    }
}
