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
        _order  = Array.Empty<int>();
    }

    public int Count { get; private set; }
    public int Start(int goal) => _start[goal];
    public int End(int goal)   => _start[goal + 1];
    public int At(int i)       => _order[i];

    public void Build(EntityBrainContext[] contexts, int count)
    {
        if (count > _order.Length)
            _order = new int[Math.Max(_order.Length << 1, count)];

        Array.Clear(_start, 0, _start.Length);

        int included = 0;
        for (int i = 0; i < count; i++)
        {
            if (contexts[i].SkipExec) continue;
            _start[(int)contexts[i].CurrentGoal + 1]++;
            included++;
        }

        for (int g = 0; g < SharedHierarchicalBrain.GoalCount; g++)
        {
            _start[g + 1] += _start[g];
            _cursor[g] = _start[g];
        }

        for (int i = 0; i < count; i++)
            if (!contexts[i].SkipExec)
                _order[_cursor[(int)contexts[i].CurrentGoal]++] = i;

        Count = included;
    }
}