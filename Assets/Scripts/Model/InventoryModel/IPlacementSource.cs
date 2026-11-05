public interface IPlacementSource
{
    bool HasPlaceableInActiveCell();
    int ActivePlaceableLevels => 1;
    byte ActivePlaceableKind => 0;
    object ActivePlaceable => null;
}
