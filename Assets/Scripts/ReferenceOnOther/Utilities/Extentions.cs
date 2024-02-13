using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TheRavine.Extentions
{
    public static class Extention
    {
        static public double JaroWinklerSimilarity(string str1, string str2)
        {
            if ((str1 == null) || (str2 == null))
                return 0;
            int matchingChars = 0;
            int transpositions = 0;
            int maxDistance = Math.Max(str1.Length, str2.Length) / 2 - 1;
            bool[] str1Matches = new bool[str1.Length];
            bool[] str2Matches = new bool[str2.Length];
            for (int i = 0; i < str1.Length; i++)
            {
                int start = Math.Max(0, i - maxDistance);
                int end = Math.Min(i + maxDistance + 1, str2.Length);
                for (int j = start; j < end; j++)
                {
                    if (!str2Matches[j] && str1[i] == str2[j])
                    {
                        str1Matches[i] = true;
                        str2Matches[j] = true;
                        matchingChars++;
                        break;
                    }
                }
            }
            if (matchingChars == 0)
                return 0;
            int k = 0;
            for (int i = 0; i < str1.Length; i++)
            {
                if (str1Matches[i])
                {
                    while (!str2Matches[k])
                        k++;
                    if (str1[i] != str2[k])
                        transpositions++;
                    k++;
                }
            }
            double jaroSimilarity = (double)matchingChars / (double)str1.Length;
            double winklerSimilarity = jaroSimilarity + ((transpositions * 0.1) * (1 - jaroSimilarity));
            return winklerSimilarity;
        }
        static public float newx;
        public static Vector2 GenerateRandomPointAround(Vector2 centerPoint, int minDistance, int maxDistance)
        {
            float distance = Random.Range(minDistance, maxDistance);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            newx = centerPoint.x + distance * Mathf.Cos(angle);
            float newy = centerPoint.y + distance * Mathf.Sin(angle);
            return new Vector2((int)newx, (int)newy);
        }

        private static Vector2 RoundVector2(Vector2 vec) => new Vector2(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
        public static Vector2 Vector2D(Vector3 vec) => new Vector2(vec.x, vec.y);
        public static Vector2 RoundVector2D(Vector3 vec) => RoundVector2(Vector2D(vec));
    }


    public class EnumerableSnapshot<T> : IEnumerable<T>, IDisposable
    {
        private IEnumerable<T> _source;
        private IEnumerator<T> _enumerator;
        private ReadOnlyCollection<T> _cached;

        public EnumerableSnapshot(IEnumerable<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_source == null) throw new ObjectDisposedException(this.GetType().Name);
            if (_enumerator == null)
            {
                _enumerator = _source.GetEnumerator();
                _cached = new ReadOnlyCollection<T>(_source.ToArray());
            }
            else
            {
                var modified = false;
                if (_source is ICollection collection)
                {
                    modified = _cached.Count != collection.Count;
                }
                if (!modified)
                {
                    try
                    {
                        _enumerator.MoveNext();
                    }
                    catch (InvalidOperationException)
                    {
                        modified = true;
                    }
                }
                if (modified)
                {
                    _enumerator.Dispose();
                    _enumerator = _source.GetEnumerator();
                    _cached = new ReadOnlyCollection<T>(_source.ToArray());
                }
            }
            return _cached.GetEnumerator();
        }

        public void Dispose()
        {
            _enumerator?.Dispose();
            _enumerator = null;
            _cached = null;
            _source = null;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class EnumerableSnapshotExtensions
    {
        public static EnumerableSnapshot<T> ToEnumerableSnapshot<T>(
            this IEnumerable<T> source) => new EnumerableSnapshot<T>(source);
    }

    public class Vector2Comparer : IComparer<Vector2>
    {
        public int Compare(Vector2 v1, Vector2 v2)
        {
            if (v1.x.CompareTo(v2.x) != 0)
                return v1.x.CompareTo(v2.x);
            return v1.y.CompareTo(v2.y);
        }
    }

    public struct Pair<T, U>
    {
        public Pair(T first, U second)
        {
            this.First = first;
            this.Second = second;
        }
        public T First { get; set; }
        public U Second { get; set; }
    };
}