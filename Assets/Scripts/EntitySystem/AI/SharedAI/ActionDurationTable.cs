public static class ActionDurationTable
{
    private static readonly float[] MinSeconds =
    {
        0.30f, 0.50f, 0.10f, 1.00f, 0.50f, 0.50f, 0.20f,
        0.50f, 0.40f, 0.10f, 1.00f, 0.30f, 0.30f
    };

    private static readonly float[] MaxSeconds =
    {
        2.00f, 4.00f, 0.30f, 6.00f, 4.00f, 5.00f, 1.00f,
        2.00f, 2.50f, 0.50f, 6.00f, 1.50f, 1.50f
    };

    public const float MinGoalSeconds = 2f;
    public const float MaxGoalSeconds = 10f;

    public static float Min(int action) =>
        (uint)action < (uint)MinSeconds.Length ? MinSeconds[action] : 0.3f;

    public static float Max(int action) =>
        (uint)action < (uint)MaxSeconds.Length ? MaxSeconds[action] : 2f;
}