using TheRavine.Generator;

public sealed class SeasonClockSource : IMapSeason
{
    public static readonly SeasonClockSource Instance = new();

    private SeasonClockSource() { }

    public float Value(double time) => SeasonClock.Value((float)time);
    public bool IsWinter(double time) => SeasonClock.IsWinter((float)time);
}
