public interface IPlacementSource
{
    bool HasPlaceableInActiveCell();
    int ActivePlaceableLevels => 1;
    object ActivePlaceable => null;
}
