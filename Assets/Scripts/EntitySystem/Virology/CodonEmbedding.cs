using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public static class CodonEmbedding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Embed(ushort codon, out float4 lo, out float4 hi)
        {
            uint c = codon;

            lo = new float4(
                math.countbits(c & 0x00FFu),
                math.countbits(c & 0x03FCu),
                math.countbits(c & 0x0FF0u),
                math.countbits(c & 0x3FC0u)) * 0.25f - 1f;

            hi = new float4(
                math.countbits(c & 0xFF00u),
                math.countbits(c & 0xFC03u),
                math.countbits(c & 0xF00Fu),
                math.countbits(c & 0xC03Fu)) * 0.25f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Translate(ushort codon, float[] centroids, float[] cellBias, float sharpness, out float amp)
        {
            int best = Nearest(codon, centroids, cellBias, out float d2);
            amp = math.exp(-d2 * sharpness);
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Nearest(ushort codon, float[] centroids, float[] cellBias, out float bestDist)
        {
            Embed(codon, out float4 lo, out float4 hi);
            var rows = MemoryMarshal.Cast<float, float4>(centroids.AsSpan());

            int best = 0;
            float bestScore = float.MaxValue;
            bestDist = 0f;

            for (int n = 0; n < ProteinTable.ActionCount; n++)
            {
                float4 a = rows[n << 1] - lo;
                float4 b = rows[(n << 1) + 1] - hi;
                float d2 = math.dot(a, a) + math.dot(b, b);
                float score = d2 - cellBias[n];
                if (score >= bestScore) continue;
                bestScore = score; bestDist = d2; best = n;
            }

            return best;
        }
    }
}