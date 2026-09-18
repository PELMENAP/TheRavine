using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public static class ViralMutator
    {
        public const float BaseMutationRate = 0.08f;
        public const float PointWeight = 0.55f;
        public const float InsertWeight = 0.15f;
        public const float DeleteWeight = 0.15f;
        public const float DuplicateWeight = 0.15f;

        public static float ResolveRate(ushort firstCodon, float[] centroids,
            float[] cellBias, float sharpness, float mutationRateDelta)
        {
            int action = CodonEmbedding.Translate(firstCodon, centroids, cellBias, sharpness, out float amp);

            float rate = (ProteinAction)action switch
            {
                ProteinAction.MutationRateUp => math.min(BaseMutationRate * (1f + amp * 3f), 0.5f),
                ProteinAction.MutationRateDown => BaseMutationRate / (1f + amp * 3f),
                _ => BaseMutationRate
            };

            return math.clamp(rate * (1f + mutationRateDelta * 2f), 0.001f, 0.5f);
        }

        public static int Transmit(ushort[] source, int start, int count,
            ushort[] destination, float rate, ref XorShift32 rng)
        {
            int written = 0;
            int capacity = destination.Length;

            for (int i = 0; i < count && written < capacity; i++)
            {
                ushort codon = source[start + i];

                if (NextUnit(ref rng) < rate)
                {
                    float roll = NextUnit(ref rng);

                    if (roll < PointWeight)
                    {
                        codon ^= (ushort)(1 << (int)(rng.NextUInt() & 15u));
                        if (NextUnit(ref rng) < 0.4f)
                            codon ^= (ushort)(1 << (int)(rng.NextUInt() & 15u));
                    }
                    else if (roll < PointWeight + InsertWeight)
                    {
                        destination[written++] = (ushort)(rng.NextUInt() & 0xFFFFu);
                        if (written >= capacity) break;
                    }
                    else if (roll < PointWeight + InsertWeight + DeleteWeight)
                    {
                        continue;
                    }
                    else
                    {
                        int span = math.min(1 + (int)(rng.NextUInt() % 4u), count - i);
                        for (int d = 0; d < span && written < capacity; d++)
                            destination[written++] = source[start + i + d];
                        if (written >= capacity) break;
                    }
                }

                destination[written++] = codon;
            }

            return written;
        }

        public static ulong ComputeStrainId(ushort[] codons, int count)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < count; i++)
            {
                hash ^= codons[i];
                hash *= 1099511628211UL;
            }
            return hash == 0UL ? 1UL : hash;
        }

        public static float NextUnit(ref XorShift32 rng)
            => (rng.NextUInt() >> 8) * (1f / 16777216f);
    }
}