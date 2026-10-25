namespace TheRavine.Generator
{
    public readonly struct LevelCell
    {
        public readonly int  LocalIdx;
        public readonly byte Level;

        public LevelCell(int localIdx, byte level)
        {
            LocalIdx = localIdx;
            Level    = level;
        }
    }

    public sealed class BuildingVolume
    {
        public readonly int Anchor;
        public readonly int Levels;
        public readonly LevelCell[] Cells;

        public BuildingVolume(int anchor, int levels, LevelCell[] cells)
        {
            Anchor = anchor;
            Levels = levels;
            Cells  = cells;
        }
    }
}
