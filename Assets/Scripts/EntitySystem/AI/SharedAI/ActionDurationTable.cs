public static class ActionDurationTable
{
    public const float MinGoalSeconds = 2f;
    public const float MaxGoalSeconds = 10f;

    public static float Min(int action)
    {
        var table = SimulationRules.Frame.ActionMinSeconds;
        return table != null && (uint)action < (uint)table.Length ? table[action] : 0.3f;
    }

    public static float Max(int action)
    {
        var table = SimulationRules.Frame.ActionMaxSeconds;
        return table != null && (uint)action < (uint)table.Length ? table[action] : 2f;
    }
}
