using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public static class ViralMutator
    {
        public static float ResolveRate(ushort firstCodon, float[] centroids,
            float[] cellBias, float sharpness, float mutationRateDelta)
        {
            int action = CodonEmbedding.Translate(firstCodon, centroids, cellBias, sharpness, out float amp);
            ref readonly var r = ref SimulationRules.Frame;
            float baseRate = r.BaseMutationRate;

            float rate = (ProteinAction)action switch
            {
                ProteinAction.MutationRateUp => math.min(baseRate * (1f + amp * r.MutationAmpGain), r.MutationRateMax),
                ProteinAction.MutationRateDown => baseRate / (1f + amp * r.MutationAmpGain),
                _ => baseRate
            };

            return math.clamp(rate * (1f + mutationRateDelta * r.MutationRateDeltaGain), r.MutationRateMin, r.MutationRateMax);
        }

        public static int Transmit(ushort[] source, int start, int count,
            ushort[] destination, float rate, ref XorShift32 rng)
        {
            int written = 0;
            int capacity = destination.Length;
            ref readonly var r = ref SimulationRules.Frame;
            float point  = r.MutationPointWeight;
            float insert = point + r.MutationInsertWeight;
            float delete = insert + r.MutationDeleteWeight;

            for (int i = 0; i < count && written < capacity; i++)
            {
                ushort codon = source[start + i];

                if (NextUnit(ref rng) < rate)
                {
                    float roll = NextUnit(ref rng);

                    if (roll < point)
                    {
                        codon ^= (ushort)(1 << (int)(rng.NextUInt() & 15u));
                        if (NextUnit(ref rng) < r.MutationDoubleFlipChance)
                            codon ^= (ushort)(1 << (int)(rng.NextUInt() & 15u));
                    }
                    else if (roll < insert)
                    {
                        destination[written++] = (ushort)(rng.NextUInt() & 0xFFFFu);
                        if (written >= capacity) break;
                    }
                    else if (roll < delete)
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