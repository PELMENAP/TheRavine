using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.EntityControl.Virology;

public static class CreatureCore
{
    public static float DayPhase(ref DayCycle dayCycle)
    {
        if (dayCycle == null && !ServiceLocator.Services.TryGet(out dayCycle)) return 0f;
        return dayCycle.NormalizedTime.CurrentValue;
    }

    public static bool IsNightPhase(float phase, in SimulationRules.RulesFrame r)
        => r.NightStart > r.NightEnd
            ? phase >= r.NightStart || phase < r.NightEnd
            : phase >= r.NightStart && phase < r.NightEnd;

    public static float EnvironmentDrainMul(float wellFed, bool exposedAtNight, float fever, bool winter,
        in SimulationRules.RulesFrame r, bool dormant = false)
        => (1f - r.WellFedBasalReduction * wellFed)
         * (exposedAtNight ? r.NightOutsideDrainMul : 1f)
         * (1f + fever * r.FeverDrainMul)
         * (dormant ? r.HibernateDrainMul : winter ? r.WinterDrainMul : 1f);

    public static float ImmuneUpkeepMul(in GeneticParameters genes, SimulationRules r)
        => 1f + genes.ImmuneInvestment * r.ImmuneUpkeep;

    public static VirologyComponent CreateVirology(in GeneticParameters genes, VirologyComponent parent, SimulationRules r)
    {
        var virology = new VirologyComponent();
        uint seed = (uint)RavineRandom.RangeInt(1, int.MaxValue);
        if (parent != null && parent.IsCreated && !parent.IsDisposed)
            virology.FillFromParent(parent, in genes, seed);
        else
            virology.FillComponent(in genes, seed);
        virology.SetImmuneStrength(math.lerp(r.ImmuneThresholdMin, r.ImmuneThresholdMax, genes.ImmuneInvestment));
        return virology;
    }

    public static void ReadVitals(StatsComponent stats, VirologyComponent virology,
        out float hp, out float energy, out float viral)
    {
        hp     = 0f;
        energy = 0f;
        viral  = 0f;
        if (stats == null || stats.IsDisposed) return;
        hp     = stats.MaxHealth > 0f ? math.saturate(stats.Hp / stats.MaxHealth) : 0f;
        energy = stats.MaxEnergy > 0f ? math.saturate(stats.En / stats.MaxEnergy) : 0f;
        virology?.TryGetViralInputs(out viral, out _, out _);
    }

    public static void StepVirology(VirologyComponent virology, StatsComponent stats, MortalityComponent mortality,
        float dt, in HostState host)
    {
        float hpBefore = stats.Hp;
        virology.Step(stats, dt, in host);
        if (stats.Hp < hpBefore) mortality.NoteHarm(DeathCause.Virus, stats.Hp);
    }

    public static void Metabolize(StatsComponent stats, MortalityComponent mortality, float dt, float movementEnergy,
        float metabolismMultiplier, float basalMul, in SimulationRules.RulesFrame r, out float metabolismCredit)
        => Metabolize(stats, mortality, dt, movementEnergy, metabolismMultiplier, basalMul, in r,
            0f, out metabolismCredit, out _, out _, out _, out _, out _, out _);

    public static void Metabolize(StatsComponent stats, MortalityComponent mortality, float dt, float movementEnergy,
        float metabolismMultiplier, float basalMul, in SimulationRules.RulesFrame r, float warmthAvailable,
        out float metabolismCredit, out float basalSpent, out float moveSpent, out float starvationReturned,
        out float clampGain, out float warmthDrawn, out float warmthEmitted)
    {
        float hpBefore = stats.Hp;
        stats.Tick(dt,
            movementEnergy,
            metabolismMultiplier,
            r.BasalEnergyDrain * basalMul,
            r.StarvationThreshold, r.StarvationDamage, r.StarvationEnergyReturn,
            warmthAvailable, r.WarmthDrawFraction, r.BasalEmitFraction,
            out metabolismCredit, out basalSpent, out moveSpent, out starvationReturned, out clampGain,
            out warmthDrawn, out warmthEmitted);
        if (stats.Hp < hpBefore) mortality.NoteHarm(DeathCause.Starved, stats.Hp);
    }
}
