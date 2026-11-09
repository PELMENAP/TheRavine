public interface IPlacementSource
{
    bool HasPlaceableInActiveCell();
    int ActivePlaceableLevels => 1;
    int ActivePlaceableObjectId => -1;
    object ActivePlaceable => null;
}
