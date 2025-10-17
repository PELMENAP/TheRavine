using System;
using ZLinq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            _cached = new ReadOnlyCollection<T>(_source.AsValueEnumerable().ToArray());
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
                _cached = new ReadOnlyCollection<T>(_source.AsValueEnumerable().ToArray());
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
        this IEnumerable<T> source) => new(source);
}