using System;
using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public struct TranslationTable : IDisposable
    {
        public float[] Centroids;
        public float Sharpness;

        public const float CentroidSpread = 0.50f;
        public const float JitterScale = 0.18f;
        public const float MutationScale = 0.12f;

        public static float[] CreatePrototype(uint seed, Allocator allocator)
        {
            var array = new float[ProteinTable.ActionCount * ProteinTable.EmbedDim];

            var rng = new XorShift32(seed);
            for (int i = 0; i < array.Length; i++)
                array[i] = NextSymmetric(ref rng) * CentroidSpread;

            return array;
        }

        public static TranslationTable CreateFrom(float[] prototype, float sharpness,
            float jitter, uint seed)
        {
            var table = new TranslationTable
            {
                Sharpness = sharpness,
                Centroids = new float[prototype.Length]
            };

            var rng = new XorShift32(seed);
            for (int i = 0; i < prototype.Length; i++)
                table.Centroids[i] = prototype[i] + NextSymmetric(ref rng) * jitter * JitterScale;

            return table;
        }

        public TranslationTable Clone(Allocator allocator)
        {
            var copy = new TranslationTable
            {
                Sharpness = Sharpness,
                Centroids = new float[Centroids.Length]
            };
            Array.Copy(Centroids, copy.Centroids, Centroids.Length);
            return copy;
        }

        public void Mutate(float chance, uint seed)
        {
            var rng = new XorShift32(seed);
            for (int i = 0; i < Centroids.Length; i++)
            {
                if (NextUnit(ref rng) >= chance) continue;
                Centroids[i] = math.clamp(
                    Centroids[i] + NextSymmetric(ref rng) * MutationScale, -1.5f, 1.5f);
            }
        }

        public void Dispose()
        {
        }

        private static float NextUnit(ref XorShift32 rng)
            => (rng.NextUInt() >> 8) * (1f / 16777216f);

        private static float NextSymmetric(ref XorShift32 rng)
            => NextUnit(ref rng) * 2f - 1f;
    }
}