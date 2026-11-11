using Unity.Mathematics;

namespace TheRavine.Generator
{
    public struct CollisionShape
    {
        public float2 Half;
        public float2 Offset;
        public float  Bottom;
        public float  Height;
        public CollisionKind Kind;

        public float Extent => Kind == CollisionKind.None
            ? 0f
            : math.length(Offset) + (Kind == CollisionKind.Cylinder ? Half.x : math.length(Half));

        public static CollisionShape Cylinder(float radius, float height, float3 offset = default) => new CollisionShape
        {
            Kind   = radius > 0f && height > 0f ? CollisionKind.Cylinder : CollisionKind.None,
            Half   = new float2(radius, radius),
            Offset = offset.xz,
            Bottom = offset.y,
            Height = height
        };

        public static CollisionShape Box(float2 half, float height, float3 offset = default) => new CollisionShape
        {
            Kind   = half.x > 0f && half.y > 0f && height > 0f ? CollisionKind.Box : CollisionKind.None,
            Half   = half,
            Offset = offset.xz,
            Bottom = offset.y,
            Height = height
        };
    }
}
