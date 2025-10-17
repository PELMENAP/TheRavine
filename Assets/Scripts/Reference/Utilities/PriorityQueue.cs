using UnityEngine;
using System.Collections.Generic;
using System;

namespace TheRavine.Extensions
{
    public class PriorityQueue<TElement, TPriority>
    {
        private readonly List<(TElement element, TPriority priority)> elements = new();
        private readonly IComparer<TPriority> comparer;

        public PriorityQueue() : this(Comparer<TPriority>.Default)
        {
        }

        public PriorityQueue(IComparer<TPriority> comparer)
        {
            this.comparer = comparer ?? Comparer<TPriority>.Default;
        }

        public int Count => elements.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            elements.Add((element, priority));
            int i = elements.Count - 1;

            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (comparer.Compare(elements[parent].priority, elements[i].priority) <= 0)
                    break;

                var temp = elements[i];
                elements[i] = elements[parent];
                elements[parent] = temp;
                i = parent;
            }
        }

        public TElement Dequeue()
        {
            if (elements.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            var result = elements[0].element;
            int lastIndex = elements.Count - 1;
            elements[0] = elements[lastIndex];
            elements.RemoveAt(lastIndex);

            lastIndex--;
            if (lastIndex > 0)
            {
                int i = 0;
                while (true)
                {
                    int smallest = i;
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;

                    if (left <= lastIndex && comparer.Compare(elements[left].priority, elements[smallest].priority) < 0)
                        smallest = left;

                    if (right <= lastIndex && comparer.Compare(elements[right].priority, elements[smallest].priority) < 0)
                        smallest = right;

                    if (smallest == i)
                        break;

                    var temp = elements[i];
                    elements[i] = elements[smallest];
                    elements[smallest] = temp;
                    i = smallest;
                }
            }

            return result;
        }

        public bool TryPeek(out TElement element, out TPriority priority)
        {
            if (elements.Count > 0)
            {
                element = elements[0].element;
                priority = elements[0].priority;
                return true;
            }

            element = default;
            priority = default;
            return false;
        }
    }


    public class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly T[] buffer;
        private int start;
        private int end;
        private int count;

        public int Count => count;
        public int Capacity => buffer.Length;

        public CircularBuffer(int capacity)
        {
            buffer = new T[capacity];
            start = 0;
            end = 0;
            count = 0;
        }

        public void Add(T item)
        {
            buffer[end] = item;
            end = (end + 1) % buffer.Length;

            if (count == buffer.Length)
            {
                start = (start + 1) % buffer.Length;
            }
            else
            {
                count++;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (count == 0) yield break;

            for (int i = 0; i < count; i++)
            {
                yield return buffer[(start + i) % buffer.Length];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}