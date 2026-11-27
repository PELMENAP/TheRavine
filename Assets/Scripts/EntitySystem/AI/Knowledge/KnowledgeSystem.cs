using System;
using Unity.Mathematics;

public sealed class KnowledgeSystem
{
    public const int Features = 6;
    public const int Observations = 32;
    public const int MinObservations = 8;
    private const int Stride = Features + 1;
    private const int Slots = 2;
    private const int Nodes = KnowledgeMath.MaxNodes;

    private readonly float[] _obs;
    private readonly int[]   _obsHead;
    private readonly int[]   _obsCount;
    private readonly float[] _pendingX;
    private readonly float[] _pendingEnergy;
    private readonly float[] _pendingTime;
    private readonly byte[]  _hasPending;
    private readonly float[] _nextObserve;
    private readonly float[] _nextSearch;
    private readonly byte[]  _ops;
    private readonly float[] _args;
    private readonly byte[]  _nodes;
    private readonly byte[]  _taught;
    private readonly byte[]  _scratchOps  = new byte[Nodes];
    private readonly float[] _scratchArgs = new float[Nodes];
    private readonly float[] _row = new float[Features];

    public int Capacity { get; }
    public long Accepted { get; private set; }
    public long Transmitted { get; private set; }
    public double RewardSum { get; private set; }

    public KnowledgeSystem(int capacity)
    {
        Capacity       = Math.Max(capacity, 1);
        _obs           = new float[Capacity * Observations * Stride];
        _obsHead       = new int[Capacity];
        _obsCount      = new int[Capacity];
        _pendingX      = new float[Capacity * Features];
        _pendingEnergy = new float[Capacity];
        _pendingTime   = new float[Capacity];
        _hasPending    = new byte[Capacity];
        _nextObserve   = new float[Capacity];
        _nextSearch    = new float[Capacity];
        _ops           = new byte[Capacity * Slots * Nodes];
        _args          = new float[Capacity * Slots * Nodes];
        _nodes         = new byte[Capacity * Slots];
        _taught        = new byte[Capacity];
    }

    public void Reset(int index)
    {
        if ((uint)index >= (uint)Capacity) return;
        _obsHead[index] = 0;
        _obsCount[index] = 0;
        _hasPending[index] = 0;
        _nextObserve[index] = 0f;
        _nextSearch[index] = 0f;
        _taught[index] = 0;
        _nodes[index * Slots] = 0;
        _nodes[index * Slots + 1] = 0;
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            Array.Copy(_obs, last * Observations * Stride, _obs, index * Observations * Stride, Observations * Stride);
            Array.Copy(_pendingX, last * Features, _pendingX, index * Features, Features);
            Array.Copy(_ops, last * Slots * Nodes, _ops, index * Slots * Nodes, Slots * Nodes);
            Array.Copy(_args, last * Slots * Nodes, _args, index * Slots * Nodes, Slots * Nodes);
            _obsHead[index]       = _obsHead[last];
            _obsCount[index]      = _obsCount[last];
            _pendingEnergy[index] = _pendingEnergy[last];
            _pendingTime[index]   = _pendingTime[last];
            _hasPending[index]    = _hasPending[last];
            _nextObserve[index]   = _nextObserve[last];
            _nextSearch[index]    = _nextSearch[last];
            _taught[index]        = _taught[last];
            _nodes[index * Slots]     = _nodes[last * Slots];
            _nodes[index * Slots + 1] = _nodes[last * Slots + 1];
        }
        Reset(last);
    }

    public int ObservationCount(int index) => (uint)index < (uint)Capacity ? _obsCount[index] : 0;

    public int BestNodes(int index) => (uint)index < (uint)Capacity ? _nodes[index * Slots] : 0;

    public bool HasTaught(int index) => (uint)index < (uint)Capacity && _taught[index] != 0;

    public float Predict(int index, ReadOnlySpan<float> x)
    {
        int n = BestNodes(index);
        if (n == 0 || x.Length < Features) return float.NaN;
        return KnowledgeMath.Evaluate(new ReadOnlySpan<byte>(_ops, index * Slots * Nodes, Nodes),
            new ReadOnlySpan<float>(_args, index * Slots * Nodes, Nodes), n, x);
    }

    public bool ObserveDue(int index, float now) => (uint)index < (uint)Capacity && now >= _nextObserve[index];

    public void Observe(int index, float now, ReadOnlySpan<float> x, float energy, float maxEnergy, in SimulationRules.RulesFrame r)
    {
        if ((uint)index >= (uint)Capacity || x.Length < Features || now < _nextObserve[index]) return;
        _nextObserve[index] = now + r.KnowledgeObserveInterval;

        if (_hasPending[index] != 0)
        {
            float dt = now - _pendingTime[index];
            if (dt > 1e-3f && maxEnergy > 0f)
            {
                float y = (energy - _pendingEnergy[index]) / (maxEnergy * dt) * r.KnowledgeTargetScale;
                if (math.isfinite(y)) Push(index, y);
            }
        }

        for (int f = 0; f < Features; f++) _pendingX[index * Features + f] = x[f];
        _pendingEnergy[index] = energy;
        _pendingTime[index]   = now;
        _hasPending[index]    = 1;
    }

    public void Push(int index, ReadOnlySpan<float> x, float y)
    {
        for (int f = 0; f < Features; f++) _pendingX[index * Features + f] = x[f];
        Push(index, y);
    }

    private void Push(int index, float y)
    {
        int head = _obsHead[index];
        int at = (index * Observations + head) * Stride;
        for (int f = 0; f < Features; f++) _obs[at + f] = _pendingX[index * Features + f];
        _obs[at + Features] = y;
        _obsHead[index] = (head + 1) % Observations;
        if (_obsCount[index] < Observations) _obsCount[index]++;
    }

    public float Search(int index, float now, in SimulationRules.RulesFrame r)
    {
        if ((uint)index >= (uint)Capacity || now < _nextSearch[index]) return 0f;
        _nextSearch[index] = now + r.KnowledgeSearchInterval;
        int n = _obsCount[index];
        if (n < MinObservations) return 0f;

        int hold = math.max(2, n / 4);
        int fit  = n - hold;

        double mean = 0d;
        for (int j = 0; j < fit; j++) mean += Target(index, n, j);
        float baseConst = (float)(mean / fit);

        int bestBase = index * Slots * Nodes;
        int bestCount = _nodes[index * Slots];
        int count;
        if (_taught[index] != 0)
        {
            _taught[index] = 0;
            count = _nodes[index * Slots + 1];
            Array.Copy(_ops, bestBase + Nodes, _scratchOps, 0, count);
            Array.Copy(_args, bestBase + Nodes, _scratchArgs, 0, count);
        }
        else
        {
            if (bestCount > 0)
            {
                Array.Copy(_ops, bestBase, _scratchOps, 0, bestCount);
                Array.Copy(_args, bestBase, _scratchArgs, 0, bestCount);
                count = bestCount;
            }
            else
            {
                _scratchOps[0] = KnowledgeMath.OpConst;
                _scratchArgs[0] = baseConst;
                count = 1;
            }
            count = KnowledgeMath.Mutate(_scratchOps, _scratchArgs, count, Features, r.KnowledgeConstScale);
        }
        if (!KnowledgeMath.IsValid(_scratchOps, _scratchArgs, count, Features)) return 0f;

        SumSquared(index, n, new ReadOnlySpan<byte>(_scratchOps), new ReadOnlySpan<float>(_scratchArgs), count, fit,
            out float candFit, out float candHold, out bool candOk);
        if (!candOk) return 0f;

        float bestFit, bestHold;
        int   incumbent;
        if (bestCount > 0)
        {
            SumSquared(index, n, new ReadOnlySpan<byte>(_ops, bestBase, Nodes), new ReadOnlySpan<float>(_args, bestBase, Nodes),
                bestCount, fit, out bestFit, out bestHold, out bool bestOk);
            if (!bestOk) { bestFit = float.MaxValue; bestHold = float.MaxValue; }
            incumbent = bestCount;
        }
        else
        {
            ConstSquared(index, n, baseConst, fit, out bestFit, out bestHold);
            incumbent = 1;
        }
        ConstSquared(index, n, baseConst, fit, out _, out float baseHold);

        float jCandFit  = KnowledgeMath.ModelCost(count, r.KnowledgeNodeCost) + KnowledgeMath.DataCost(candFit, fit, r.KnowledgeNoiseFloor);
        float jBestFit  = KnowledgeMath.ModelCost(incumbent, r.KnowledgeNodeCost) + KnowledgeMath.DataCost(bestFit, fit, r.KnowledgeNoiseFloor);
        float jCandHold = KnowledgeMath.ModelCost(count, r.KnowledgeNodeCost) + KnowledgeMath.DataCost(candHold, hold, r.KnowledgeNoiseFloor);
        float jBestHold = KnowledgeMath.ModelCost(incumbent, r.KnowledgeNodeCost) + KnowledgeMath.DataCost(bestHold, hold, r.KnowledgeNoiseFloor);
        float jBaseHold = KnowledgeMath.ModelCost(1, r.KnowledgeNodeCost) + KnowledgeMath.DataCost(baseHold, hold, r.KnowledgeNoiseFloor);

        if (!(jCandFit + r.KnowledgeMinGain < jBestFit) || !(jCandHold <= jBestHold)) return 0f;

        Array.Copy(_scratchOps, 0, _ops, bestBase, count);
        Array.Copy(_scratchArgs, 0, _args, bestBase, count);
        _nodes[index * Slots] = (byte)count;
        Accepted++;

        if (!(jCandHold < jBaseHold)) return 0f;
        float reward = math.min(r.KnowledgeRewardScale * math.max(0f, jBestHold - jCandHold) / hold, r.KnowledgeRewardCap);
        RewardSum += reward;
        return reward;
    }

    public bool Teach(int from, int to, in SimulationRules.RulesFrame r)
    {
        if ((uint)from >= (uint)Capacity || (uint)to >= (uint)Capacity || from == to) return false;
        int count = _nodes[from * Slots];
        if (count == 0) return false;
        int src = from * Slots * Nodes;
        int dst = to * Slots * Nodes + Nodes;
        Array.Copy(_ops, src, _ops, dst, count);
        Array.Copy(_args, src, _args, dst, count);
        for (int k = 0; k < count; k++)
            if (_ops[dst + k] == KnowledgeMath.OpConst && RavineRandom.RangeFloat() < r.KnowledgeCopyError)
                _args[dst + k] += RavineRandom.RangeFloat(-1f, 1f) * r.KnowledgeConstScale;
        _nodes[to * Slots + 1] = (byte)count;
        _taught[to] = 1;
        Transmitted++;
        return true;
    }

    private float Target(int index, int n, int j)
        => _obs[Row(index, n, j) + Features];

    private int Row(int index, int n, int j)
        => (index * Observations + (_obsHead[index] - n + j + Observations) % Observations) * Stride;

    private void SumSquared(int index, int n, ReadOnlySpan<byte> ops, ReadOnlySpan<float> args, int count, int fit,
        out float fitSq, out float holdSq, out bool ok)
    {
        double f = 0d, h = 0d;
        ok = true;
        for (int j = 0; j < n; j++)
        {
            int at = Row(index, n, j);
            for (int k = 0; k < Features; k++) _row[k] = _obs[at + k];
            float p = KnowledgeMath.Evaluate(ops, args, count, _row);
            if (!math.isfinite(p)) { ok = false; break; }
            float d = p - _obs[at + Features];
            if (j < fit) f += d * d; else h += d * d;
        }
        fitSq  = (float)math.min(f, float.MaxValue);
        holdSq = (float)math.min(h, float.MaxValue);
        ok &= math.isfinite(fitSq) && math.isfinite(holdSq);
    }

    private void ConstSquared(int index, int n, float c, int fit, out float fitSq, out float holdSq)
    {
        double f = 0d, h = 0d;
        for (int j = 0; j < n; j++)
        {
            float d = c - Target(index, n, j);
            if (j < fit) f += d * d; else h += d * d;
        }
        fitSq  = (float)f;
        holdSq = (float)h;
    }
}
