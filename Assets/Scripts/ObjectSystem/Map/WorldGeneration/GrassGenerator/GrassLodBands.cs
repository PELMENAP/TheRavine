using System;

namespace TheRavine.Generator
{
    public static class GrassLodBands
    {
        public const int FarRings = 2;
        public const int BandCount = FarRings + 1;
        public const float MinWidthScale = 0.25f;
        public const float MaxWidthScale = 16f;

        public static int ValidStep(int step, int windowSide)
        {
            if (windowSide <= 0 || step <= 1) return 1;

            int maxStep = windowSide & -windowSide;
            int result = 1;
            while (result < step && result < maxStep) result <<= 1;
            return result;
        }

        public static float ValidWidthScale(float scale)
        {
            if (!(scale > MinWidthScale)) return MinWidthScale;
            return scale > MaxWidthScale ? MaxWidthScale : scale;
        }

        public static int CellsPerSide(int windowSide, int step) =>
            windowSide <= 0 ? 0 : windowSide / ValidStep(step, windowSide);

        public static int Capacity(int windowSide, int step, int bladesPerCell, int placedCapacity)
        {
            if (windowSide <= 0 || bladesPerCell <= 0 || placedCapacity <= 0) return 0;

            long side = CellsPerSide(windowSide, step);
            long capacity = side * side * bladesPerCell;
            return (int)Math.Min(capacity, placedCapacity);
        }
    }
}
