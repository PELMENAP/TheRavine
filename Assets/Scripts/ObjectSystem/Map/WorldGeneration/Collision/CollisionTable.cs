using System;
using Unity.Collections;
using Unity.Mathematics;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public sealed class CollisionTable : IDisposable
    {
        private NativeArray<CollisionShape> shapes;

        public float MaxExtent { get; private set; }
        public int ShapeCount { get; private set; }

        public ObstacleSet Set => new ObstacleSet { Shapes = shapes, MaxExtent = MaxExtent };

        public CollisionTable(ObjectInfoRegistry registry, float buildStep, float levelHeight)
        {
            int capacity = registry != null ? registry.IdCapacity : 0;
            shapes = new NativeArray<CollisionShape>(math.max(capacity, 1), Allocator.Persistent);

            for (int id = 0; id < capacity; id++)
            {
                ObjectInfo info = registry.Get(id);
                if (info == null) continue;

                CollisionShape shape = info.BakeCollision(buildStep, levelHeight);
                shapes[id] = shape;
                if (shape.Kind == CollisionKind.None) continue;

                ShapeCount++;
                MaxExtent = math.max(MaxExtent, shape.Extent);
            }
        }

        public void Dispose()
        {
            if (shapes.IsCreated) shapes.Dispose();
            MaxExtent = 0f;
            ShapeCount = 0;
        }
    }
}
