using System;
using Unity.Collections;
using Unity.Mathematics;

public sealed class ColonyRegistry : IDisposable
{
    public const int MaxColonies = byte.MaxValue + 1;

    private ColonyState[] _colonies = Array.Empty<ColonyState>();
    private int _count;
    private int _nextId;
    private NativeArray<ColonyFieldView> _views;

    public int Count => _count;
    public ColonyState this[int index] => _colonies[index];
    public NativeArray<ColonyFieldView> Views => _views;

    public ColonyState Add(float2 position, SharedHierarchicalBrain brain, int memberCapacity)
    {
        if (_count >= MaxColonies)
            throw new InvalidOperationException($"ColonyRegistry: превышен лимит {MaxColonies} колоний");

        if (_count == _colonies.Length) Array.Resize(ref _colonies, math.max(4, _colonies.Length << 1));

        var colony = new ColonyState(_count, ++_nextId, position, brain, memberCapacity);
        _colonies[_count++] = colony;
        RebuildViews();
        return colony;
    }

    public ColonyState Nearest(float2 p)
    {
        ColonyState best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < _count; i++)
        {
            float d = math.distancesq(_colonies[i].Nest.Position, p);
            if (d >= bestD) continue;
            bestD = d;
            best  = _colonies[i];
        }
        return best;
    }

    private void RebuildViews()
    {
        if (!_views.IsCreated || _views.Length < _count)
        {
            if (_views.IsCreated) _views.Dispose();
            _views = new NativeArray<ColonyFieldView>(math.max(_colonies.Length, 1), Allocator.Persistent);
        }
        for (int i = 0; i < _count; i++) _views[i] = _colonies[i].Nest.View;
    }

    public void Dispose()
    {
        for (int i = 0; i < _count; i++)
        {
            _colonies[i].Dispose();
            _colonies[i] = null;
        }
        _count = 0;
        if (_views.IsCreated) _views.Dispose();
    }
}
