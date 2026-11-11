using Unity.Collections;

namespace TheRavine.Generator
{
    public struct ObstacleSet
    {
        [ReadOnly] public NativeArray<CollisionShape> Shapes;
        public float MaxExtent;

        public bool Any => MaxExtent > 0f;
    }
}
