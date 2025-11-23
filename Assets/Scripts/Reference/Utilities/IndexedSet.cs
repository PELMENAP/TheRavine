using UnityEngine;
using System.Collections.Generic;
using System;

namespace TheRavine.Extensions
{
    public class IndexedSet<T>
    {
        private List<T> items = new();
        private Dictionary<T, int> itemIndices = new();

        public int Count => items.Count;
        public T this[int index] => items[index];

        public bool Add(T item)
        {
            if (itemIndices.ContainsKey(item))
                return false;
            
            itemIndices[item] = items.Count;
            items.Add(item);
            return true;
        }

        public bool Remove(T item)
        {
            if (!itemIndices.TryGetValue(item, out int index))
                return false;

            int lastIndex = items.Count - 1;
            if (index != lastIndex)
            {
                T lastItem = items[lastIndex];
                items[index] = lastItem;
                itemIndices[lastItem] = index;
            }

            items.RemoveAt(lastIndex);
            itemIndices.Remove(item);
            return true;
        }

        public T GetRandom(FastRandom random) => items.Count > 0 ? items[random.Next(items.Count)] : default;

        public void Clear()
        {
            items.Clear();
            itemIndices.Clear();
        }
    }
}