public static class SimulationClock
{
    private static double _time;

    public static float  Time  => (float)_time;
    public static double TimeD => _time;

    public static void SetTime(double time)
    {
        if (time > _time) _time = time;
    }

    public static void Advance(float dt)
    {
        if (dt > 0f) _time += dt;
    }

    public static void Reset() => _time = 0d;
}