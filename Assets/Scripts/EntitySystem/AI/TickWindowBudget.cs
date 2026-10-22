using Unity.Mathematics;

public struct TickWindowBudget
{
    public const float WindowSeconds = 1f;
    public const float MinFrameDt    = 0.001f;
    public const int   MaxFrames     = 240;

    private double _windowStart;
    private int    _frames;

    public int FramesCounted => _frames;

    public void CountFrame() => _frames++;

    public int BeginWindow(double now, float fallbackDt, int count)
    {
        float dt = _frames > 0 ? (float)((now - _windowStart) / _frames) : fallbackDt;
        _windowStart = now;
        _frames      = 0;
        return BatchSize(count, Frames(dt));
    }

    public static int Frames(float dt)
        => math.clamp((int)math.ceil(WindowSeconds / math.max(dt, MinFrameDt)), 1, MaxFrames);

    public static int BatchSize(int count, int frames)
        => count > 0 ? (count + frames - 1) / frames : 1;
}
