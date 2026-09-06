using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class IntDictionary<TValue> : IEnumerable<KeyValuePair<int, TValue>>
{
    private const byte Empty = 0;
    private const byte Full  = 1;
    private const byte Dead  = 2;

    private const int MinCapacity = 16;
    private const int MaxLoadPercent = 75;

    private byte[]   _states;
    private int[]    _keys;
    private TValue[] _values;

    private int _mask;
    private int _threshold;
    private int _count;
    private int _used;

    public IntDictionary() : this(0) { }

    public IntDictionary(int capacity)
    {
        int cap = capacity <= MinCapacity ? MinCapacity
                                               : NextPow2(capacity + (capacity >> 1) + 1);
        _states   = new byte[cap];
        _keys     = new int[cap];
        _values   = new TValue[cap];
        _mask      = cap - 1;
        _threshold = (int)(cap * (long)MaxLoadPercent / 100);
    }

    public int Count    => _count;
    public int Capacity => _mask + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(int key)
    {
        uint h = (uint)key;
        h ^= h >> 16;   h *= 0x85ebca6bU;
        h ^= h >> 13;   h *= 0xc2b2ae35U;
        h ^= h >> 16;
        return (int)h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(int key, out TValue value)
    {
        byte[] states = _states;
        int[] keys     = _keys;
        TValue[] values = _values;
        int mask = _mask;

        int i = Hash(key) & mask;
        for (;;)
        {
            byte s = states[i];
            if (s == Empty) break;
            if (s == Full && keys[i] == key)
            {
                value = values[i];
                return true;
            }
            i = (i + 1) & mask;
        }

        value = default(TValue);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(int key) => TryGetValue(key, out _);

    public TValue this[int key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TryGetValue(key, out TValue v) ? v : ThrowMissing(key);
        set => Insert(key, value, InsertMode.Upsert);
    }

    public void Add(int key, TValue value) => Insert(key, value, InsertMode.Add);
    public bool TryAdd(int key, TValue value) => Insert(key, value, InsertMode.TryAdd);

    public TValue GetOrAdd(int key, TValue fallback)
    {
        if (TryGetValue(key, out TValue v)) return v;
        Insert(key, fallback, InsertMode.Upsert);
        return fallback;
    }

    public TValue GetOrAdd(int key, Func<int, TValue> factory)
    {
        if (TryGetValue(key, out TValue v)) return v;
        TValue created = factory(key);
        Insert(key, created, InsertMode.Upsert);
        return created;
    }

    private bool Insert(int key, TValue value, InsertMode mode)
    {
        if (_used >= _threshold)
        {
            int cap = _mask + 1;
            Rehash(_count * 2 >= _threshold ? cap << 1 : cap);
        }

        byte[] states = _states;
        int[] keys     = _keys;
        TValue[] values = _values;
        int mask = _mask;

        int i = Hash(key) & mask;
        int firstDead = -1;

        for (;;)
        {
            byte s = states[i];

            if (s == Empty)
            {
                int slot = firstDead >= 0 ? firstDead : i;
                states[slot] = Full;
                keys[slot]   = key;
                values[slot] = value;
                _count++;
                if (firstDead < 0) _used++;
                return true;
            }

            if (s == Full && keys[i] == key)
            {
                if (mode == InsertMode.Upsert) { values[i] = value; return false; }
                if (mode == InsertMode.Add)    ThrowDuplicate(key);
                return false;
            }

            if (s == Dead && firstDead < 0) firstDead = i;
            i = (i + 1) & mask;
        }
    }

    public bool Remove(int key)
    {
        byte[] states = _states;
        int[] keys     = _keys;
        int mask = _mask;

        int i = Hash(key) & mask;
        for (;;)
        {
            byte s = states[i];
            if (s == Empty) return false;

            if (s == Full && keys[i] == key)
            {
                states[i]  = Dead;
                _values[i] = default(TValue);
                _count--;
                DefragmentIfNeeded();
                return true;
            }
            i = (i + 1) & mask;
        }
    }

    public bool TryRemove(int key, out TValue value)
    {
        byte[] states = _states;
        int[] keys     = _keys;
        int mask = _mask;

        int i = Hash(key) & mask;
        for (;;)
        {
            byte s = states[i];
            if (s == Empty) { value = default(TValue); return false; }

            if (s == Full && keys[i] == key)
            {
                value = _values[i];
                states[i]  = Dead;
                _values[i] = default(TValue);
                _count--;
                DefragmentIfNeeded();
                return true;
            }
            i = (i + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DefragmentIfNeeded()
    {
        if (_used - _count > (_mask + 1) >> 3)
            Rehash(_mask + 1);
    }

    public void Clear()
    {
        Array.Clear(_states, 0, _states.Length);
        Array.Clear(_values, 0, _values.Length);
        _count = 0;
        _used = 0;
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= _count) return;
        int need = NextPow2(capacity + (capacity >> 1) + 1);
        if (need > _mask + 1) Rehash(need);
    }

    public void Compact()
    {
        int cap = _mask + 1;
        int target = NextPow2(_count + (_count >> 1) + 1);
        if (target > cap) target = cap;
        if (target == cap && _used == _count) return;
        Rehash(target);
    }

    private void Rehash(int newCapacity)
    {
        byte[]   oldStates = _states;
        int[]    oldKeys   = _keys;
        TValue[] oldValues = _values;

        _states   = new byte[newCapacity];
        _keys     = new int[newCapacity];
        _values   = new TValue[newCapacity];
        _mask      = newCapacity - 1;
        _threshold = (int)(newCapacity * (long)MaxLoadPercent / 100);
        _used      = _count;

        int mask = _mask;
        byte[] states = _states;

        for (int i = 0; i < oldStates.Length; i++)
        {
            if (oldStates[i] != Full) continue;

            int key = oldKeys[i];
            int j = Hash(key) & mask;
            while (states[j] == Full) j = (j + 1) & mask;

            states[j] = Full;
            _keys[j]   = key;
            _values[j] = oldValues[i];
        }
    }

    private static int NextPow2(int v)
    {
        int p = MinCapacity;
        while (p < v && p < (1 << 30)) p <<= 1;
        return p;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TValue ThrowMissing(int key) =>
        throw new KeyNotFoundException($"IntDictionary: key {key} not found");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDuplicate(int key) =>
        throw new ArgumentException($"IntDictionary: duplicate key {key}");

    private enum InsertMode { Upsert, Add, TryAdd }


    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<int, TValue>> IEnumerable<KeyValuePair<int, TValue>>.GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<KeyValuePair<int, TValue>>
    {
        private readonly IntDictionary<TValue> _dict;
        private int _next;
        private KeyValuePair<int, TValue> _current;

        internal Enumerator(IntDictionary<TValue> dict)
        {
            _dict = dict;
            _next = 0;
            _current = default(KeyValuePair<int, TValue>);
        }

        public KeyValuePair<int, TValue> Current => _current;
        object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            IntDictionary<TValue> d = _dict;
            byte[] states = d._states;

            for (int i = _next; i < states.Length; i++)
            {
                if (states[i] != Full) continue;
                _next = i + 1;
                _current = new KeyValuePair<int, TValue>(d._keys[i], d._values[i]);
                return true;
            }
            _next = states.Length;
            return false;
        }

        void IEnumerator.Reset() => _next = 0;
        public void Dispose() { }
    }
}