using System;
using Unity.Collections;
using Unity.Mathematics;

public struct HelpEntry
{
    public int   Partner;
    public float Balance;
    public float ComfortAccum;
    public float BalanceTime;
    public float ComfortTime;
    public float GaveTime;
    public float ReceivedTime;
    public float MetTime;
}

public sealed class HelpLog : IDisposable
{
    public const int MaxSize = 32;
    public const float Never = float.NegativeInfinity;

    private readonly int _capacity;
    private readonly int _size;
    private NativeArray<HelpEntry> _entries;

    public int Size => _size;
    public int Capacity => _capacity;

    public HelpLog(int capacity, int size)
    {
        _capacity = math.max(capacity, 1);
        _size = math.clamp(size, 1, MaxSize);
        _entries = new NativeArray<HelpEntry>(_capacity * _size, Allocator.Persistent);
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)_capacity) return;
        int b = index * _size;
        for (int i = 0; i < _size; i++) _entries[b + i] = default;
    }

    public HelpEntry Read(int index, int slot) => _entries[index * _size + slot];

    public int Count(int index)
    {
        if ((uint)index >= (uint)_capacity) return 0;
        int b = index * _size, n = 0;
        for (int i = 0; i < _size; i++) if (_entries[b + i].Partner != 0) n++;
        return n;
    }

    public int Find(int index, int partner)
    {
        if ((uint)index >= (uint)_capacity || partner == 0) return -1;
        int b = index * _size;
        for (int i = 0; i < _size; i++) if (_entries[b + i].Partner == partner) return i;
        return -1;
    }

    private int Touch(int index, int partner, float now)
    {
        int b = index * _size;
        int free = -1, oldest = 0;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < _size; i++)
        {
            var e = _entries[b + i];
            if (e.Partner == partner) return i;
            if (e.Partner == 0)
            {
                if (free < 0) free = i;
                continue;
            }
            if (e.MetTime < oldestTime)
            {
                oldestTime = e.MetTime;
                oldest = i;
            }
        }

        int slot = free >= 0 ? free : oldest;
        _entries[b + slot] = new HelpEntry
        {
            Partner = partner, BalanceTime = now, ComfortTime = Never, GaveTime = Never, ReceivedTime = Never, MetTime = now,
        };
        return slot;
    }

    private static float Decay(float value, float from, float now, float tau)
        => value == 0f ? 0f : value * math.exp(-math.max(now - from, 0f) / math.max(tau, 1e-3f));

    public void NoteGiven(int index, int partner, float amount, float now, float tau)
    {
        if ((uint)index >= (uint)_capacity || partner == 0 || !(amount > 0f) || !math.isfinite(amount)) return;
        int i = index * _size + Touch(index, partner, now);
        var e = _entries[i];
        e.Balance = Decay(e.Balance, e.BalanceTime, now, tau) - amount;
        e.BalanceTime = now;
        e.GaveTime = now;
        e.MetTime = now;
        _entries[i] = e;
    }

    public void NoteReceived(int index, int partner, float amount, float now, float tau)
    {
        if ((uint)index >= (uint)_capacity || partner == 0 || !(amount > 0f) || !math.isfinite(amount)) return;
        int i = index * _size + Touch(index, partner, now);
        var e = _entries[i];
        e.Balance = Decay(e.Balance, e.BalanceTime, now, tau) + amount;
        e.BalanceTime = now;
        e.ReceivedTime = now;
        e.MetTime = now;
        _entries[i] = e;
    }

    public void NoteComfort(int index, int partner, float relief, float now, float window)
    {
        if ((uint)index >= (uint)_capacity || partner == 0 || !(relief >= 0f) || !math.isfinite(relief)) return;
        int i = index * _size + Touch(index, partner, now);
        var e = _entries[i];
        e.ComfortAccum = Decay(e.ComfortAccum, e.ComfortTime, now, window) + relief;
        e.ComfortTime = now;
        e.MetTime = now;
        _entries[i] = e;
    }

    public float Balance(int index, int partner, float now, float tau)
    {
        int slot = Find(index, partner);
        if (slot < 0) return 0f;
        var e = _entries[index * _size + slot];
        return Decay(e.Balance, e.BalanceTime, now, tau);
    }

    public float ComfortAccum(int index, int partner, float now, float window)
    {
        int slot = Find(index, partner);
        if (slot < 0) return 0f;
        var e = _entries[index * _size + slot];
        return e.ComfortAccum <= 0f ? 0f : Decay(e.ComfortAccum, e.ComfortTime, now, window);
    }

    public bool ComfortReady(int index, int partner, float now, float cooldown)
    {
        int slot = Find(index, partner);
        return slot < 0 || now - _entries[index * _size + slot].ComfortTime >= cooldown;
    }

    public bool GaveWithin(int index, int partner, float now, float window)
    {
        int slot = Find(index, partner);
        return slot >= 0 && now - _entries[index * _size + slot].GaveTime <= window;
    }

    public bool ReceivedWithin(int index, int partner, float now, float window)
    {
        int slot = Find(index, partner);
        return slot >= 0 && now - _entries[index * _size + slot].ReceivedTime <= window;
    }

    public void Restore(int index, in HelpEntry entry)
    {
        if ((uint)index >= (uint)_capacity || entry.Partner == 0) return;
        int slot = Find(index, entry.Partner);
        int b = index * _size;
        if (slot < 0)
            for (int i = 0; i < _size; i++)
                if (_entries[b + i].Partner == 0)
                {
                    slot = i;
                    break;
                }
        if (slot < 0) return;
        _entries[b + slot] = entry;
    }

    public float SumAbsBalance(int index, float now, float tau, out int entries)
    {
        entries = 0;
        if ((uint)index >= (uint)_capacity) return 0f;
        int b = index * _size;
        float sum = 0f;
        for (int i = 0; i < _size; i++)
        {
            var e = _entries[b + i];
            if (e.Partner == 0) continue;
            entries++;
            sum += math.abs(Decay(e.Balance, e.BalanceTime, now, tau));
        }
        return sum;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)_capacity || (uint)last >= (uint)_capacity) return;
        if (index != last)
        {
            int to = index * _size, from = last * _size;
            for (int i = 0; i < _size; i++) _entries[to + i] = _entries[from + i];
        }
        Reset(last);
    }

    public void Dispose()
    {
        if (_entries.IsCreated) _entries.Dispose();
    }
}
