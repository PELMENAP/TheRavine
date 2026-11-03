namespace TheRavine.Generator
{
    public enum PlacementResult : byte
    {
        Ok = 0,
        NoChunk = 1,
        OutOfBounds = 2,
        Occupied = 3,
        UpperLevelOccupied = 4,
        CrossChunk = 5,
        TooManyCells = 6,
        InvalidLevels = 7
    }
}
