using Unity.Mathematics;

public class DigestionComponent : IComponent
{
    public float Stomach  { get; private set; }
    public float Capacity { get; private set; }
    public float WellFed  { get; private set; }

    public float Fill => Capacity > 0f ? Stomach / Capacity : 0f;

    public void Configure(float capacity)
    {
        Capacity = math.max(capacity, 1e-3f);
        Stomach  = 0f;
        WellFed  = 0f;
    }

    public float Ingest(float energy)
    {
        if (energy <= 0f) return 0f;
        float taken = math.min(energy, Capacity - Stomach);
        if (taken <= 0f) return 0f;
        Stomach += taken;
        return taken;
    }

    public float Withdraw(float amount)
    {
        float taken = math.clamp(amount, 0f, Stomach);
        Stomach -= taken;
        return taken;
    }

    public float TakeAll()
    {
        float s = Stomach;
        Stomach = 0f;
        return s;
    }

    public float Tick(StatsComponent stats, float dt, bool resting, float rateMul, float boostMul,
        in SimulationRules.RulesFrame r, out float boostCredit)
    {
        boostCredit = 0f;
        if (dt <= 0f) return 0f;

        float rate = (resting ? r.DigestRateRest : r.DigestRateActive) * rateMul;
        float eff  = resting ? r.DigestEffRest : r.DigestEffActive;

        float baseMove  = math.min(Stomach, rate * dt);
        float boostMove = math.min(Stomach, rate * math.max(boostMul, 0f) * dt);

        float room  = math.max(0f, stats.MaxEnergy - stats.En);
        float moved = math.min(boostMove, eff > 0f ? room / eff : 0f);

        Stomach  -= moved;
        stats.En += moved * eff;
        boostCredit = math.max(0f, moved - math.min(baseMove, moved)) * eff;

        if (resting && Stomach > 0f) WellFed = math.min(1f, WellFed + r.WellFedGainPerSecond * dt);
        else WellFed = math.max(0f, WellFed - r.WellFedDecayPerSecond * dt);

        return moved;
    }

    public void Dispose() { }
}
