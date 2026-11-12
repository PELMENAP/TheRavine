using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static unsafe class ObstacleQuery
    {
        public const int MaxCellReach = 2;
        public const int MaxSweepSteps = 16;
        private const float Epsilon = 1e-8f;
        private const float NoSupport = -1e30f;

        private interface IShapeVisitor
        {
            float2 Anchor { get; }
            bool Visit(in ObjectInstInfo obj);
        }

        public static float SweepReach(float radius) => radius * MaxSweepSteps;

        public static bool Resolve<TLookup>(ref TLookup lookup, in ObstacleSet set, ref float2 position, float radius,
            float feetY, float headY, float stepUp, out float2 normal)
            where TLookup : struct, IObstacleLookup
        {
            normal = float2.zero;
            if (!set.Any) return false;

            var visitor = new PushVisitor
            {
                Set = set,
                Position = position,
                Radius = radius,
                FeetY = feetY,
                HeadY = headY,
                StepUp = stepUp
            };
            if (!Walk(ref lookup, in set, position, radius, ref visitor)) return false;

            position = visitor.Position;
            normal = math.normalizesafe(visitor.Normal);
            return true;
        }

        public static bool ResolveSwept<TLookup>(ref TLookup lookup, in ObstacleSet set, float2 origin, ref float2 position,
            float radius, float feetY, float headY, float stepUp, out float2 normal)
            where TLookup : struct, IObstacleLookup
        {
            float2 delta = position - origin;
            float len2 = math.lengthsq(delta);
            if (len2 <= radius * radius || radius <= Epsilon)
                return Resolve(ref lookup, in set, ref position, radius, feetY, headY, stepUp, out normal);

            normal = float2.zero;
            if (!set.Any) return false;

            float len = math.sqrt(len2);
            int steps = (int)math.ceil(len / radius);
            bool capped = steps > MaxSweepSteps;
            if (capped) steps = MaxSweepSteps;

            float2 stride = delta * ((capped ? radius : len / steps) / len);
            float2 target = position;
            float2 p = origin;

            for (int i = 1; i <= steps; i++)
            {
                p = i == steps && !capped ? target : origin + stride * i;
                if (!Resolve(ref lookup, in set, ref p, radius, feetY, headY, stepUp, out normal)) continue;

                position = p;
                return true;
            }

            position = p;
            return false;
        }

        public static bool Support<TLookup>(ref TLookup lookup, in ObstacleSet set, float2 position, float radius,
            float feetY, float stepUp, out float top)
            where TLookup : struct, IObstacleLookup
        {
            top = 0f;
            if (!set.Any) return false;

            var visitor = new SupportVisitor
            {
                Set = set,
                Position = position,
                Radius = radius,
                FeetY = feetY,
                StepUp = stepUp,
                Top = NoSupport
            };
            if (!Walk(ref lookup, in set, position, radius, ref visitor)) return false;

            top = visitor.Top;
            return true;
        }

        private static bool Walk<TLookup, TVisitor>(ref TLookup lookup, in ObstacleSet set, float2 center, float radius,
            ref TVisitor visitor)
            where TLookup : struct, IObstacleLookup
            where TVisitor : struct, IShapeVisitor
        {
            const float inv = MapGenerator.InvScale;
            float reach = radius + set.MaxExtent;
            int cx = (int)math.floor(center.x * inv);
            int cz = (int)math.floor(center.y * inv);
            int x0 = math.max((int)math.floor((center.x - reach) * inv), cx - MaxCellReach);
            int x1 = math.min((int)math.floor((center.x + reach) * inv), cx + MaxCellReach);
            int z0 = math.max((int)math.floor((center.y - reach) * inv), cz - MaxCellReach);
            int z1 = math.min((int)math.floor((center.y + reach) * inv), cz + MaxCellReach);

            long cachedKey = long.MinValue;
            ObstacleChunk chunk = default;
            bool has = false;
            bool hit = false;
            long s0 = long.MinValue, s1 = long.MinValue, s2 = long.MinValue, s3 = long.MinValue;
            int scannedCount = 0;

            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                long key = MapGenerator.CellToChunk(x, z);
                if (key != cachedKey)
                {
                    cachedKey = key;
                    has = lookup.TryGetObstacles(key, out chunk);
                }
                if (!has) continue;

                int idx = MapGenerator.CellToLocalIdx(x, z);
                int handle = chunk.Occupancy[idx];
                if (handle > 0 && handle <= chunk.Objects->Length)
                    hit |= visitor.Visit(in chunk.Objects->Ptr[handle - 1]);

                if (chunk.LevelMask[idx] == 0 || *chunk.Elevated == 0) continue;
                if (key == s0 || key == s1 || key == s2 || key == s3) continue;
                switch (scannedCount++)
                {
                    case 0: s0 = key; break;
                    case 1: s1 = key; break;
                    case 2: s2 = key; break;
                    default: s3 = key; break;
                }
                hit |= WalkElevated(in chunk, reach, ref visitor);
            }

            return hit;
        }

        private static bool WalkElevated<TVisitor>(in ObstacleChunk chunk, float reach, ref TVisitor visitor)
            where TVisitor : struct, IShapeVisitor
        {
            bool hit = false;
            ObjectInstInfo* objects = chunk.Objects->Ptr;
            int count = chunk.Objects->Length;
            float reach2 = reach * reach;

            for (int i = 0; i < count; i++)
            {
                if (objects[i].BaseLevel == 0) continue;
                float2 anchor = visitor.Anchor;
                float dx = objects[i].Position.x - anchor.x;
                float dz = objects[i].Position.z - anchor.y;
                if (dx * dx + dz * dz > reach2) continue;
                hit |= visitor.Visit(in objects[i]);
            }
            return hit;
        }

        private struct PushVisitor : IShapeVisitor
        {
            public ObstacleSet Set;
            public float2 Position;
            public float2 Normal;
            public float Radius;
            public float FeetY;
            public float HeadY;
            public float StepUp;

            public float2 Anchor => Position;

            public bool Visit(in ObjectInstInfo obj)
            {
                int id = obj.PrefabID;
                if ((uint)id >= (uint)Set.Shapes.Length) return false;

                CollisionShape shape = Set.Shapes[id];
                if (shape.Kind == CollisionKind.None) return false;

                float bottom = obj.Position.y + shape.Bottom;
                if (bottom + shape.Height <= FeetY + StepUp || HeadY <= bottom) return false;

                float2 center = new float2(obj.Position.x, obj.Position.z) + shape.Offset;
                float2 d = Position - center;
                float2 n;

                if (shape.Kind == CollisionKind.Cylinder)
                {
                    float sum = Radius + shape.Half.x;
                    float d2 = math.lengthsq(d);
                    if (d2 >= sum * sum) return false;

                    n = d2 > Epsilon ? d * math.rsqrt(d2) : new float2(1f, 0f);
                    Position = center + n * sum;
                    Normal += n;
                    return true;
                }

                float2 q = math.clamp(d, -shape.Half, shape.Half);
                float2 e = d - q;
                float e2 = math.lengthsq(e);
                if (e2 >= Radius * Radius) return false;

                if (e2 > Epsilon)
                {
                    n = e * math.rsqrt(e2);
                    Position = center + q + n * Radius;
                    Normal += n;
                    return true;
                }

                float2 depth = shape.Half - math.abs(d);
                if (depth.x < depth.y)
                {
                    n = new float2(d.x >= 0f ? 1f : -1f, 0f);
                    Position = new float2(center.x + n.x * (shape.Half.x + Radius), Position.y);
                }
                else
                {
                    n = new float2(0f, d.y >= 0f ? 1f : -1f);
                    Position = new float2(Position.x, center.y + n.y * (shape.Half.y + Radius));
                }
                Normal += n;
                return true;
            }
        }

        private struct SupportVisitor : IShapeVisitor
        {
            public ObstacleSet Set;
            public float2 Position;
            public float Radius;
            public float FeetY;
            public float StepUp;
            public float Top;

            public float2 Anchor => Position;

            public bool Visit(in ObjectInstInfo obj)
            {
                int id = obj.PrefabID;
                if ((uint)id >= (uint)Set.Shapes.Length) return false;

                CollisionShape shape = Set.Shapes[id];
                if (shape.Kind == CollisionKind.None) return false;

                float top = obj.Position.y + shape.Bottom + shape.Height;
                if (top <= Top || top > FeetY + StepUp) return false;

                float2 d = Position - (new float2(obj.Position.x, obj.Position.z) + shape.Offset);
                if (shape.Kind == CollisionKind.Cylinder)
                {
                    float sum = Radius + shape.Half.x;
                    if (math.lengthsq(d) >= sum * sum) return false;
                }
                else
                {
                    float2 e = d - math.clamp(d, -shape.Half, shape.Half);
                    if (math.lengthsq(e) >= Radius * Radius) return false;
                }

                Top = top;
                return true;
            }
        }
    }
}
