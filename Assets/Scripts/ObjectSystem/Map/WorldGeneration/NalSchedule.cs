using System;

namespace TheRavine.Generator
{
    public sealed class NalSchedule
    {
        public struct Entry
        {
            public double Due;
            public long Pos;
            public int PrefabId;
        }

        private Entry[] heap;
        private int count;

        public NalSchedule(int capacity)
        {
            heap = new Entry[Math.Max(capacity, 4)];
        }

        public int Count => count;
        public int Capacity => heap.Length;

        public void Push(double due, long pos, int prefabId)
        {
            if (count == heap.Length)
                Array.Resize(ref heap, count * 2);

            int i = count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heap[parent].Due <= due) break;
                heap[i] = heap[parent];
                i = parent;
            }

            heap[i] = new Entry { Due = due, Pos = pos, PrefabId = prefabId };
        }

        public bool TryPopDue(double now, out Entry entry)
        {
            if (count == 0 || heap[0].Due > now)
            {
                entry = default;
                return false;
            }

            entry = heap[0];
            Entry last = heap[--count];
            if (count > 0)
            {
                int i = 0;
                while (true)
                {
                    int child = (i << 1) + 1;
                    if (child >= count) break;
                    if (child + 1 < count && heap[child + 1].Due < heap[child].Due) child++;
                    if (heap[child].Due >= last.Due) break;
                    heap[i] = heap[child];
                    i = child;
                }
                heap[i] = last;
            }

            return true;
        }

        public int CountDue(double now)
        {
            if (count == 0 || heap[0].Due > now) return 0;

            Span<int> stack = stackalloc int[64];
            int top = 0;
            int due = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                int i = stack[--top];
                due++;
                int left = (i << 1) + 1;
                if (left < count && heap[left].Due <= now) stack[top++] = left;
                if (left + 1 < count && heap[left + 1].Due <= now) stack[top++] = left + 1;
            }

            return due;
        }

        public void Clear() => count = 0;
    }
}
