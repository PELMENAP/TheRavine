using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static unsafe class ObstacleQuery
    {
        public const int MaxCellReach = 2;
        private const float Epsilon = 1e-8f;

        public static bool Resolve<TLookup>(ref TLookup lookup, in ObstacleSet set, ref float2 position, float radius,
            float feetY, float headY, out float2 normal)
            where TLookup : struct, IObstacleLookup
        {
            normal = float2.zero;
            if (!set.Any) return false;

            const float inv = MapGenerator.InvScale;
            float reach = radius + set.MaxExtent;
            int cx = (int)math.floor(position.x * inv);
            int cz = (int)math.floor(position.y * inv);
            int x0 = math.max((int)math.floor((position.x - reach) * inv), cx - MaxCellReach);
            int x1 = math.min((int)math.floor((position.x + reach) * inv), cx + MaxCellReach);
            int z0 = math.max((int)math.floor((position.y - reach) * inv), cz - MaxCellReach);
            int z1 = math.min((int)math.floor((position.y + reach) * inv), cz + MaxCellReach);

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
                    hit |= Push(in chunk.Objects->Ptr[handle - 1], in set, ref position, radius, feetY, headY, ref normal);

                if (chunk.LevelMask[idx] == 0 || *chunk.Elevated == 0) continue;
                if (key == s0 || key == s1 || key == s2 || key == s3) continue;
                switch (scannedCount++)
                {
                    case 0: s0 = key; break;
                    case 1: s1 = key; break;
                    case 2: s2 = key; break;
                    default: s3 = key; break;
                }
                hit |= PushElevated(in chunk, in set, ref position, radius, reach, feetY, headY, ref normal);
            }

            if (hit) normal = math.normalizesafe(normal);
            return hit;
        }

        private static bool PushElevated(in ObstacleChunk chunk, in ObstacleSet set, ref float2 position, float radius,
            float reach, float feetY, float headY, ref float2 normal)
        {
            bool hit = false;
            ObjectInstInfo* objects = chunk.Objects->Ptr;
            int count = chunk.Objects->Length;
            float reach2 = reach * reach;

            for (int i = 0; i < count; i++)
            {
                if (objects[i].BaseLevel == 0) continue;
                float dx = objects[i].Position.x - position.x;
                float dz = objects[i].Position.z - position.y;
                if (dx * dx + dz * dz > reach2) continue;
                hit |= Push(in objects[i], in set, ref position, radius, feetY, headY, ref normal);
            }
            return hit;
        }

        private static bool Push(in ObjectInstInfo obj, in ObstacleSet set, ref float2 position, float radius,
            float feetY, float headY, ref float2 normal)
        {
            int id = obj.PrefabID;
            if ((uint)id >= (uint)set.Shapes.Length) return false;

            CollisionShape shape = set.Shapes[id];
            if (shape.Kind == CollisionKind.None) return false;

            float bottom = obj.Position.y + shape.Bottom;
            if (feetY >= bottom + shape.Height || headY <= bottom) return false;

            float2 center = new float2(obj.Position.x, obj.Position.z) + shape.Offset;
            float2 d = position - center;
            float2 n;

            if (shape.Kind == CollisionKind.Cylinder)
            {
                float sum = radius + shape.Half.x;
                float d2 = math.lengthsq(d);
                if (d2 >= sum * sum) return false;

                n = d2 > Epsilon ? d * math.rsqrt(d2) : new float2(1f, 0f);
                position = center + n * sum;
                normal += n;
                return true;
            }

            float2 q = math.clamp(d, -shape.Half, shape.Half);
            float2 e = d - q;
            float e2 = math.lengthsq(e);
            if (e2 >= radius * radius) return false;

            if (e2 > Epsilon)
            {
                n = e * math.rsqrt(e2);
                position = center + q + n * radius;
                normal += n;
                return true;
            }

            float2 depth = shape.Half - math.abs(d);
            if (depth.x < depth.y)
            {
                n = new float2(d.x >= 0f ? 1f : -1f, 0f);
                position = new float2(center.x + n.x * (shape.Half.x + radius), position.y);
            }
            else
            {
                n = new float2(0f, d.y >= 0f ? 1f : -1f);
                position = new float2(position.x, center.y + n.y * (shape.Half.y + radius));
            }
            normal += n;
            return true;
        }
    }
}
