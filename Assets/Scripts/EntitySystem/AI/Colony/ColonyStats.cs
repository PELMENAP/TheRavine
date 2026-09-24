using Unity.Mathematics;

public sealed class ColonyStats
{
    public int   Born;
    public int   Died;
    public float LifespanSum;
    public float LifespanEma;
    public bool  HasLifespan;

    public readonly int[] DeathsByCause   = new int[(int)DeathCause.Age + 1];
    public readonly int[] CommandsBySource = new int[(int)CommandSource.Count];
    public readonly int[] PlansStarted    = new int[PlanCatalog.Count];
    public readonly int[] PlansCompleted  = new int[PlanCatalog.Count];

    private float  _deathRateEma;
    private double _lastDeathTime = double.NaN;
    public float DeathRatePerMinute => _deathRateEma;

    public float MeanLifespan => Died > 0 ? LifespanSum / Died : 0f;

    public void RecordBirth() => Born++;

    public void RecordDeath(float lifespan, DeathCause cause)
    {
        Died++;
        LifespanSum += lifespan;
        DeathsByCause[(int)cause]++;

        float a = SimulationRules.Active.StatsEmaAlpha;
        LifespanEma = HasLifespan ? math.lerp(LifespanEma, lifespan, a) : lifespan;
        HasLifespan = true;

        double now = SimulationClock.TimeD;
        if (!double.IsNaN(_lastDeathTime))
        {
            float gap  = (float)math.max(now - _lastDeathTime, 1e-3);
            float rate = 60f / gap;
            _deathRateEma = math.lerp(_deathRateEma, rate, a);
        }
        _lastDeathTime = now;
    }

    public void RecordCommand(CommandSource source) => CommandsBySource[(int)source]++;

    public void RecordPlan(PlanKind plan, bool completed)
    {
        if (plan >= PlanKind.Count) return;
        PlansStarted[(int)plan]++;
        if (completed) PlansCompleted[(int)plan]++;
    }

    public float SourceFraction(CommandSource source)
    {
        int total = 0;
        for (int i = 0; i < CommandsBySource.Length; i++) total += CommandsBySource[i];
        return total > 0 ? CommandsBySource[(int)source] / (float)total : 0f;
    }
}
