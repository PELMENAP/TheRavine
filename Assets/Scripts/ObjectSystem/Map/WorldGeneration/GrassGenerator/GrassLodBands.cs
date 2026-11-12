using System;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class GrassLodBands
    {
        public const int BandCount = 3;
        public const float MaxDistance = 100000f;

        private const double CellDiagonal = 1.4142135623730951;

        public static Vector3 ClampEnds(Vector3 ends)
        {
            float e0 = ClampDistance(ends.x);
            float e1 = Math.Max(ClampDistance(ends.y), e0);
            float e2 = Math.Max(ClampDistance(ends.z), e1);
            return new Vector3(e0, e1, e2);
        }

        public static Vector3 ClampKeep(Vector3 keep)
        {
            float k0 = ClampFraction(keep.x);
            float k1 = Math.Min(ClampFraction(keep.y), k0);
            float k2 = Math.Min(ClampFraction(keep.z), k1);
            return new Vector3(k0, k1, k2);
        }

        public static Vector4 PackEnds(Vector3 ends, float fadeWidth)
        {
            Vector3 e = ClampEnds(ends);
            return new Vector4(e.x, e.y, e.z, ClampDistance(fadeWidth));
        }

        public static Vector4 PackKeep(Vector3 keep)
        {
            Vector3 k = ClampKeep(keep);
            return new Vector4(k.x, k.y, k.z, 0f);
        }

        public static int Capacity(int band, Vector3 ends, int windowSide, int bladesPerCell, int placedCapacity)
        {
            if ((uint)band >= BandCount || windowSide <= 0 || bladesPerCell <= 0 || placedCapacity <= 0) return 0;

            Vector3 e = ClampEnds(ends);
            double lo = band == 0 ? 0.0 : e[band - 1];
            double hi = e[band];
            if (!(hi > lo)) return 0;

            double outer = hi + CellDiagonal;
            double inner = Math.Max(lo - CellDiagonal, 0.0);
            double cells = Math.Ceiling(Math.PI * (outer * outer - inner * inner));
            cells = Math.Min(cells, (double)windowSide * windowSide);

            long capacity = (long)cells * bladesPerCell;
            return (int)Math.Min(capacity, placedCapacity);
        }

        private static float ClampDistance(float value)
        {
            if (!(value > 0f)) return 0f;
            return value > MaxDistance ? MaxDistance : value;
        }

        private static float ClampFraction(float value)
        {
            if (!(value > 0f)) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
