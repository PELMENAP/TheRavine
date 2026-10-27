using TheRavine.Generator;

namespace TheRavine.ObjectControl
{
    public static class ConstructionService
    {
        public static PlacementResult Place(MapGenerator generator, long position, ObjectInfo info, int amount)
        {
            if (info == null || info.Levels <= 1) return PlacementResult.InvalidLevels;

            PlacementResult result = generator.TryAddFromInfo(position, generator.GetRealPosition(position), info, amount, info.Levels);
            if (result == PlacementResult.Ok) generator.ExtraUpdate();
            return result;
        }
    }
}
