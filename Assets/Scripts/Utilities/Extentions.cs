using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
public static class Extentions
{
    static public double JaroWinklerSimilarity(string str1, string str2)
    {
        if ((str1 == null) || (str2 == null))
        {
            return 0;
        }
        int matchingChars = 0;
        int transpositions = 0;
        // Вычисление максимальной разницы для определения границы сравнения
        int maxDistance = Math.Max(str1.Length, str2.Length) / 2 - 1;
        // Массивы для хранения информации о совпадающих символах
        bool[] str1Matches = new bool[str1.Length];
        bool[] str2Matches = new bool[str2.Length];
        // Поиск совпадающих символов
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
        // Если нет совпадающих символов, сходство равно 0
        if (matchingChars == 0)
        {
            return 0;
        }
        // Вычисление количества транспозиций
        int k = 0;
        for (int i = 0; i < str1.Length; i++)
        {
            if (str1Matches[i])
            {
                while (!str2Matches[k])
                {
                    k++;
                }
                if (str1[i] != str2[k])
                {
                    transpositions++;
                }
                k++;
            }
        }
        // Вычисление коэффициента сходства Джаро-Винкдера
        double jaroSimilarity = (double)matchingChars / (double)str1.Length;
        double winklerSimilarity = jaroSimilarity + ((transpositions * 0.1) * (1 - jaroSimilarity));
        return winklerSimilarity;
    }

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