using System;
using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public struct Tape : IDisposable
    {
        public ushort[] Codons;
        public int Length;
        public int Head;

        public int Capacity => Codons.Length;

        public static Tape Create(int capacity, Allocator allocator) => new()
        {
            Codons = new ushort[capacity],
            Length = 0,
            Head = 0
        };

        public bool TryInsert(int index, ushort[] source, int sourceStart, int count)
        {
            if (count <= 0 || Length + count > Capacity) return false;
            index = math.clamp(index, 0, Length);

            for (int i = Length - 1; i >= index; i--)
                Codons[i + count] = Codons[i];

            for (int i = 0; i < count; i++)
                Codons[index + i] = source[sourceStart + i];

            Length += count;
            if (Head >= index) Head += count;
            return true;
        }

        public void Remove(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= Length) return;
            count = math.min(count, Length - index);

            for (int i = index + count; i < Length; i++)
                Codons[i - count] = Codons[i];

            Length -= count;
            if (Length <= 0) { Length = 0; Head = 0; return; }
            if (Head >= index + count) Head -= count;
            else if (Head >= index) Head = index;
            if (Head >= Length) Head = 0;
        }

        public void Advance(int maxTapeLength, bool primed)
        {
            if (Length <= 0) { Head = 0; return; }

            int step = (primed || Length <= maxTapeLength) ? 1 : 2;
            Head += step;

            if (Head >= Length)
            {
                Head -= Length;
                if (step == 2 && (Length & 1) == 0) Head++;
                if (Head >= Length) Head -= Length;
            }
        }

        public ulong ComputeHash(int start, int count)
        {
            ulong hash = 14695981039346656037UL;
            int end = math.min(start + count, Length);
            for (int i = start; i < end; i++)
            {
                hash ^= Codons[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }

        public void Dispose()
        {
        }
    }
}