using System;

public sealed class DelayedItem
{
    public int   DecisionId;
    public int   Predicted;
    public float Evaluation;
    public float ValueEstimate;
    public float LogProbability;
    public float DurationLogit;
    public float DurationNoise;
    public float Duration;
    public float StartTime;
    public int   BpttSlot;
    public bool  RewardApplied;
    public bool  Trained;
    public int   CreatedOrdinal;
    public int   StepsElapsed;
    public int WeightVersion;

    public readonly float[] State;
    public readonly float[] Probs;
    public float HeadingSin;
    public float HeadingCos;
    public readonly float[] AuxNoise = new float[KernelLayout.MaxAux];
    public readonly float[] AuxValue = new float[KernelLayout.MaxAux];
    public float ExplorationEpsilon;
    

    public DelayedItem(int stateSize, int actionCount)
    {
        State = new float[stateSize];
        Probs = new float[actionCount];
    }
    public int BpttStamp;

    public void Reset()
    {
        DecisionId = 0;
        Predicted = -1;
        Evaluation = 0f;
        ValueEstimate = 0f;
        LogProbability = 0f;
        ExplorationEpsilon = 0f;
        DurationLogit = 0f;
        DurationNoise = 0f;
        Duration = 0f;
        StartTime = 0f;
        BpttSlot = 0;
        BpttStamp = 0;
        CreatedOrdinal = 0;
        StepsElapsed = 0;
        WeightVersion = 0;
        RewardApplied = false;
        Trained = false;
        HeadingSin = 0f;
        HeadingCos = 0f;
        System.Array.Clear(AuxNoise, 0, AuxNoise.Length);
        System.Array.Clear(AuxValue, 0, AuxValue.Length);
    }
}

public sealed class DecisionRing
{
    private readonly DelayedItem[] _items;
    private readonly int[] _slotById;
    private readonly int  _mask;
    private readonly bool _pow2;

    private int _head;
    private int _count;

    public int Count => _count;
    public int Capacity => _items.Length;

    public DecisionRing(int capacity, int stateSize, int actionCount)
    {
        if (capacity < 2) capacity = 2;

        _items = new DelayedItem[capacity];
        for (int i = 0; i < capacity; i++)
            _items[i] = new DelayedItem(stateSize, actionCount);

        _pow2 = (capacity & (capacity - 1)) == 0;
        _mask = capacity - 1;
        _slotById = new int[capacity];
    }

    private int Wrap(int i) => _pow2 ? (i & _mask) : i % _items.Length;

    public DelayedItem this[int index] => _items[Wrap(_head + index)];

    public DelayedItem Oldest => _count > 0 ? _items[_head] : null;

    public DelayedItem Newest => _count > 0 ? _items[Wrap(_head + _count - 1)] : null;

    public DelayedItem Push(int decisionId)
    {
        if (_count == _items.Length)
        {
            _head = Wrap(_head + 1);
            _count--;
        }

        int slot = Wrap(_head + _count);
        var item = _items[slot];
        _count++;

        item.Reset();
        item.DecisionId = decisionId;

        if (_pow2) _slotById[decisionId & _mask] = slot;

        return item;
    }

    public void PopOldest()
    {
        if (_count == 0) return;
        _head = Wrap(_head + 1);
        _count--;
    }

    public DelayedItem Find(int decisionId)
    {
        if (_pow2)
        {
            int slot = _slotById[decisionId & _mask];

            int rel = slot - _head;
            if (rel < 0) rel += _items.Length;
            if (rel >= _count) return null;

            var item = _items[slot];
            return item.DecisionId == decisionId ? item : null;
        }

        for (int i = 0; i < _count; i++)
        {
            var item = _items[(_head + i) % _items.Length];
            if (item.DecisionId == decisionId) return item;
        }
        return null;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_slotById, 0, _slotById.Length);
    }
}