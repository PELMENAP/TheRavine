using System;
using System.Runtime.InteropServices;

namespace TheRavine.EntityControl.Virology
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Segment
    {
        public int Start;
        public int Length;
        public ulong StrainId;
        public ulong LineageId;
        public ulong Signature;
        public uint InsertTick;
        public float Integrity;
        public float NetFitnessDelta;
        public float PrevFitnessDelta;
        public int TamedTicks;
        public ProteinAction DominantAction;
        public bool Tamed;

        public bool IsEndogenous => StrainId == 0UL;

        public static ulong ComputeSignature(ushort[] codons, int start, int count)
        {
            if (codons == null || count <= 0) return 0UL;

            Span<int> acc = stackalloc int[64];
            acc.Clear();

            int features = count >= 3 ? count - 2 : 1;
            for (int f = 0; f < features; f++)
            {
                int i = start + f;
                ulong key = codons[i];
                if (count > 1) key |= (ulong)codons[i + 1] << 16;
                if (count > 2) key |= (ulong)codons[i + 2] << 32;

                ulong h = Mix(key);
                for (int b = 0; b < 64; b++)
                    acc[b] += ((int)(h >> b) & 1) * 2 - 1;
            }

            ulong sig = 0UL;
            for (int b = 0; b < 64; b++)
                if (acc[b] > 0) sig |= 1UL << b;
            return sig;
        }

        private static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}