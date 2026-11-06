using TheRavine.Generator;

namespace TheRavine.ObjectControl
{
    public static class ConstructionService
    {
        public static PlacementResult Place(MapGenerator generator, long position, ObjectInfo info, int amount, int baseLevel = 0)
        {
            if (info == null) return PlacementResult.InvalidLevels;

            PlacementResult result = generator.TryPlaceSnapped(position, info, amount, baseLevel);
            if (result == PlacementResult.Ok) generator.ExtraUpdate();
            return result;
        }
    }
}
