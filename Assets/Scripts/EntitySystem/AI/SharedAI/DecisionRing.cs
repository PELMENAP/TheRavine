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

    public readonly float[] State;
    public readonly float[] Probs;

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
        DurationLogit = 0f;
        DurationNoise = 0f;
        Duration = 0f;
        StartTime = 0f;
        BpttSlot = 0;
        BpttStamp = 0;
        RewardApplied = false;
        Trained = false;
    }
}

public sealed class DecisionRing
{
    private readonly DelayedItem[] _items;
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
    }

    public DelayedItem this[int index] => _items[(_head + index) % _items.Length];

    public DelayedItem Oldest => _count > 0 ? _items[_head] : null;

    public DelayedItem Newest => _count > 0 ? _items[(_head + _count - 1) % _items.Length] : null;

    public DelayedItem Push()
    {
        if (_count == _items.Length)
        {
            _head = (_head + 1) % _items.Length;
            _count--;
        }
        var item = _items[(_head + _count) % _items.Length];
        _count++;
        item.Reset();
        return item;
    }

    public void PopOldest()
    {
        if (_count == 0) return;
        _head = (_head + 1) % _items.Length;
        _count--;
    }

    public DelayedItem Find(int decisionId)
    {
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
    }
}