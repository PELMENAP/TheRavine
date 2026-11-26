using System;
using Unity.Mathematics;

public sealed class AnomalyDetector
{
    public static AnomalyDetector Current { get; private set; }

    private readonly int _capacity;
    private readonly double _loopWindow;
    private readonly float _reliefThreshold;
    private readonly float _minInvestment;

    private readonly int[] _giver;
    private readonly int[] _receiver;
    private readonly float[] _remaining;
    private readonly double[] _time;
    private int _head;
    private int _count;

    private readonly int _tableMask;
    private readonly long[] _pairKey;
    private readonly float[] _pairSum;
    private readonly bool[] _pairUsed;
    private readonly bool[] _pairFlagged;
    private int _pairCount;

    private readonly int[] _voteLaw;
    private readonly int[] _voteAuthor;
    private readonly float[] _voteAmount;
    private int _voteCount;

    private int _loops;
    private int _farm;

    public int TransferLoops => _loops;
    public int ReliefFarm => _farm;
    public int DroppedPairs { get; private set; }
    public int DroppedVotes { get; private set; }

    public AnomalyDetector(int capacity, float loopWindow, float reliefPairThreshold, float minInvestment)
    {
        _capacity = math.max(capacity, 1);
        _loopWindow = math.max(loopWindow, 0f);
        _reliefThreshold = math.max(reliefPairThreshold, 0f);
        _minInvestment = math.max(minInvestment, 0f);

        _giver = new int[_capacity];
        _receiver = new int[_capacity];
        _remaining = new float[_capacity];
        _time = new double[_capacity];

        int table = math.ceilpow2(_capacity * 2);
        _tableMask = table - 1;
        _pairKey = new long[table];
        _pairSum = new float[table];
        _pairUsed = new bool[table];
        _pairFlagged = new bool[table];

        _voteLaw = new int[_capacity];
        _voteAuthor = new int[_capacity];
        _voteAmount = new float[_capacity];
    }

    public void Bind() => Current = this;

    public void Unbind()
    {
        if (ReferenceEquals(Current, this)) Current = null;
    }

    public void NoteTransfer(int giverId, int receiverId, float amount, double time)
    {
        if (giverId == receiverId || !(amount > 0f) || !float.IsFinite(amount)) return;

        float left = amount;
        for (int k = 0; k < _count && left > 0f; k++)
        {
            int i = (_head - 1 - k + _capacity * 2) % _capacity;
            if (time - _time[i] > _loopWindow) break;
            if (_remaining[i] <= 0f || _giver[i] != receiverId || _receiver[i] != giverId) continue;

            float used = math.min(_remaining[i], left);
            _remaining[i] -= used;
            left -= used;
            if (_remaining[i] <= 1e-6f)
            {
                _remaining[i] = 0f;
                _loops++;
            }
        }

        _giver[_head] = giverId;
        _receiver[_head] = receiverId;
        _remaining[_head] = amount;
        _time[_head] = time;
        _head = (_head + 1) % _capacity;
        if (_count < _capacity) _count++;
    }

    public void NoteRelief(int giverId, int receiverId, float amount)
    {
        if (giverId == receiverId || !(amount > 0f) || !float.IsFinite(amount)) return;

        long key = ((long)(uint)giverId << 32) | (uint)receiverId;
        int slot = unchecked((int)((ulong)key * 0x9E3779B97F4A7C15UL >> 40)) & _tableMask;
        for (int probe = 0; probe <= _tableMask; probe++, slot = (slot + 1) & _tableMask)
        {
            if (!_pairUsed[slot])
            {
                if (_pairCount >= _capacity)
                {
                    DroppedPairs++;
                    return;
                }
                _pairUsed[slot] = true;
                _pairKey[slot] = key;
                _pairCount++;
            }
            else if (_pairKey[slot] != key) continue;

            _pairSum[slot] += amount;
            if (!_pairFlagged[slot] && _pairSum[slot] > _reliefThreshold)
            {
                _pairFlagged[slot] = true;
                _farm++;
            }
            return;
        }
        DroppedPairs++;
    }

    public void NoteInvestment(int lawId, int authorId, float energy)
    {
        if (!(energy > 0f) || !float.IsFinite(energy)) return;

        for (int i = 0; i < _voteCount; i++)
        {
            if (_voteLaw[i] != lawId || _voteAuthor[i] != authorId) continue;
            _voteAmount[i] += energy;
            return;
        }

        if (_voteCount >= _capacity)
        {
            DroppedVotes++;
            return;
        }
        _voteLaw[_voteCount] = lawId;
        _voteAuthor[_voteCount] = authorId;
        _voteAmount[_voteCount] = energy;
        _voteCount++;
    }

    public float SampleConcentration(WorldCodexService codex, out int laws)
    {
        if (codex != null)
        {
            for (int i = _voteCount - 1; i >= 0; i--)
            {
                if (codex.TryGetLaw(_voteLaw[i], out _)) continue;
                int last = --_voteCount;
                _voteLaw[i] = _voteLaw[last];
                _voteAuthor[i] = _voteAuthor[last];
                _voteAmount[i] = _voteAmount[last];
            }
        }

        laws = 0;
        double sum = 0d;
        for (int i = 0; i < _voteCount; i++)
        {
            int law = _voteLaw[i];
            bool first = true;
            for (int j = 0; j < i; j++)
                if (_voteLaw[j] == law)
                {
                    first = false;
                    break;
                }
            if (!first) continue;

            double total = 0d, squares = 0d;
            for (int j = i; j < _voteCount; j++)
            {
                if (_voteLaw[j] != law) continue;
                total += _voteAmount[j];
                squares += (double)_voteAmount[j] * _voteAmount[j];
            }
            if (total < _minInvestment || total <= 0d) continue;
            sum += squares / (total * total);
            laws++;
        }
        return laws > 0 ? (float)(sum / laws) : 0f;
    }

    public void ConsumeWindow(out int loops, out int farm)
    {
        loops = _loops;
        farm = _farm;
        _loops = 0;
        _farm = 0;
        Array.Clear(_pairUsed, 0, _pairUsed.Length);
        Array.Clear(_pairFlagged, 0, _pairFlagged.Length);
        Array.Clear(_pairSum, 0, _pairSum.Length);
        _pairCount = 0;
    }
}
