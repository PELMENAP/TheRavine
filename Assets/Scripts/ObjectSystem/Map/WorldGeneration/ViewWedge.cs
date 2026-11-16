using System;
using Unity.Mathematics;

using TheRavine.ViewControl;

namespace TheRavine.Generator
{
    public struct ViewBlock
    {
        public int X;
        public int Z;
        public int Ring;
        public float Lateral;
    }

    public static class ViewWedge
    {
        public const float FrustumHalfDegrees = 52f;
        public const float MarginDegrees = ViewSector.HalfDegrees + ViewSector.HysteresisDegrees;
        public const float Base = 1.5f;
        public const float Back = 0.75f;
        public const int MaxBlockRadius = 8;

        private const int Side = 2 * MaxBlockRadius + 1;

        public static readonly float Slope;

        private static readonly bool[] Table = new bool[ViewSector.Count * Side * Side];

        static ViewWedge()
        {
            Slope = math.tan(math.radians(FrustumHalfDegrees + MarginDegrees));

            for (int s = 0; s < ViewSector.Count; s++)
            {
                ViewSector sector = ViewSector.FromIndex(s);
                for (int bz = -MaxBlockRadius; bz <= MaxBlockRadius; bz++)
                for (int bx = -MaxBlockRadius; bx <= MaxBlockRadius; bx++)
                    Table[(s * Side + bz + MaxBlockRadius) * Side + bx + MaxBlockRadius] =
                        Test(sector.Forward, sector.Right, bx, bz);
            }
        }

        public static bool Test(float2 forward, float2 right, int bx, int bz)
        {
            if (bx == 0 && bz == 0) return true;

            float2 delta = new(bx, bz);
            float f = math.dot(delta, forward);
            float l = math.abs(math.dot(delta, right));
            return f >= -Back - 1e-4f && l <= Base + Slope * math.max(f, 0f) + 1e-4f;
        }

        public static bool Contains(ViewSector sector, int bx, int bz)
        {
            if (bx == 0 && bz == 0) return true;
            if (math.abs(bx) > MaxBlockRadius || math.abs(bz) > MaxBlockRadius) return false;
            return Table[(sector.Index * Side + bz + MaxBlockRadius) * Side + bx + MaxBlockRadius];
        }

        public static int Ring(int bx, int bz) => math.max(math.abs(bx), math.abs(bz));

        public static int Collect(ViewSector sector, int ringCount, ViewBlock[] output)
        {
            ringCount = math.min(ringCount, MaxBlockRadius);
            float2 right = sector.Right;
            int n = 0;

            for (int bz = -ringCount; bz <= ringCount; bz++)
            for (int bx = -ringCount; bx <= ringCount; bx++)
            {
                int ring = Ring(bx, bz);
                if (ring == 0 || !Contains(sector, bx, bz)) continue;
                if (n == output.Length) throw new InvalidOperationException("ViewWedge.Collect: output buffer is too small");

                var block = new ViewBlock
                {
                    X = bx,
                    Z = bz,
                    Ring = ring,
                    Lateral = math.abs(math.dot(new float2(bx, bz), right))
                };

                int i = n++;
                while (i > 0 && Before(block, output[i - 1]))
                {
                    output[i] = output[i - 1];
                    i--;
                }
                output[i] = block;
            }

            return n;
        }

        public static int MaxBlocks(int ringCount)
        {
            ringCount = math.min(ringCount, MaxBlockRadius);
            int max = 0;
            for (int s = 0; s < ViewSector.Count; s++)
            {
                ViewSector sector = ViewSector.FromIndex(s);
                int n = 0;
                for (int bz = -ringCount; bz <= ringCount; bz++)
                for (int bx = -ringCount; bx <= ringCount; bx++)
                    if ((bx != 0 || bz != 0) && Contains(sector, bx, bz)) n++;
                max = math.max(max, n);
            }
            return max;
        }

        public static int MaxBlocksInRing(int ring)
        {
            if (ring < 1 || ring > MaxBlockRadius) return 0;

            int max = 0;
            for (int s = 0; s < ViewSector.Count; s++)
            {
                ViewSector sector = ViewSector.FromIndex(s);
                int n = 0;
                for (int bz = -ring; bz <= ring; bz++)
                for (int bx = -ring; bx <= ring; bx++)
                    if (Ring(bx, bz) == ring && Contains(sector, bx, bz)) n++;
                max = math.max(max, n);
            }
            return max;
        }

        private static bool Before(in ViewBlock a, in ViewBlock b) =>
            a.Ring != b.Ring ? a.Ring < b.Ring : a.Lateral < b.Lateral;
    }
}
