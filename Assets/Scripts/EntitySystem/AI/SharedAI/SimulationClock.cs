public static class SimulationClock
{
    private static float _time;

    public static float Time => _time;

    public static void SetTime(float time)
    {
        if (time > _time) _time = time;
    }

    public static void Advance(float dt)
    {
        if (dt > 0f) _time += dt;
    }

    public static void Reset() => _time = 0f;
}