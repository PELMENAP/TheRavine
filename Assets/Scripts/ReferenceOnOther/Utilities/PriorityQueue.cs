using UnityEngine;
using System.Collections.Generic;
using System;

namespace TheRavine.Extensions
{
    public class PriorityQueue<TElement, TPriority>
    {
        private readonly struct Node
        {
            public readonly TElement Element;
            public readonly TPriority Priority;

            public Node(TElement element, TPriority priority)
            {
                Element = element;
                Priority = priority;
            }
        }

        private readonly List<Node> _heap = new List<Node>();
        private readonly IComparer<TPriority> _comparer;

        public int Count => _heap.Count;

        public PriorityQueue(int capacity = 0) : this(capacity, Comparer<TPriority>.Default) { }

        public PriorityQueue(int capacity, IComparer<TPriority> comparer)
        {
            _heap = new List<Node>(capacity);
            _comparer = comparer ?? Comparer<TPriority>.Default;
        }

        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add(new Node(element, priority));
            SiftUp(_heap.Count - 1);
        }

        public TElement Dequeue()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            var top = _heap[0];
            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            SiftDown(0);
            return top.Element;
        }

        public TElement Peek()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Queue is empty");
            
            return _heap[0].Element;
        }

        public void UpdatePriority(TElement element, TPriority newPriority)
        {
            for (int i = 0; i < _heap.Count; i++)
            {
                if (EqualityComparer<TElement>.Default.Equals(_heap[i].Element, element))
                {
                    var oldPriority = _heap[i].Priority;
                    _heap[i] = new Node(element, newPriority);

                    if (_comparer.Compare(newPriority, oldPriority) < 0)
                        SiftUp(i);
                    else
                        SiftDown(i);

                    return;
                }
            }
        }

        public void Clear()
        {
            _heap.Clear();
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (Compare(index, parentIndex) >= 0) break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int smallest = index;

                if (leftChild < _heap.Count && Compare(leftChild, smallest) < 0)
                    smallest = leftChild;

                if (rightChild < _heap.Count && Compare(rightChild, smallest) < 0)
                    smallest = rightChild;

                if (smallest == index) break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private int Compare(int i, int j) => 
            _comparer.Compare(_heap[i].Priority, _heap[j].Priority);

        private void Swap(int i, int j)
        {
            var temp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = temp;
        }
    }
}