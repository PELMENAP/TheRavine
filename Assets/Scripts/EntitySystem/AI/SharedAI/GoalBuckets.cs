using System;

public sealed class GoalBuckets
{
    private readonly int[] _start;
    private readonly int[] _cursor;
    private int[] _order;

    public GoalBuckets()
    {
        _start  = new int[SharedHierarchicalBrain.GoalCount + 1];
        _cursor = new int[SharedHierarchicalBrain.GoalCount];
        _order  = new int[64];
    }

    public int Count { get; private set; }
    public int Start(int goal) => _start[goal];
    public int End(int goal)   => _start[goal + 1];
    public int At(int i)       => _order[i];

    public void Build(EntityModel[] snapshot, int count)
    {
        if (count > _order.Length)
        {
            int cap = _order.Length;
            while (cap < count) cap <<= 1;
            _order = new int[cap];
        }

        Array.Clear(_start, 0, _start.Length);

        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            _start[(int)e.Brain.Context.CurrentGoal + 1]++;
        }

        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            _start[g + 1] += _start[g];
            _cursor[g] = _start[g];
        }

        int written = 0;
        for (int i = 0; i < count; i++)
        {
            var e = snapshot[i];
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            _order[_cursor[(int)e.Brain.Context.CurrentGoal]++] = i;
            written++;
        }

        Count = written;
    }
}