using Unity.Collections.LowLevel.Unsafe;

namespace TheRavine.Generator
{
    public unsafe struct ObstacleChunk
    {
        public int* Occupancy;
        public byte* LevelMask;
        public int* Elevated;
        public UnsafeList<ObjectInstInfo>* Objects;
    }
}
