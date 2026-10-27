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
        in SimulationRules.RulesFrame r)
        => (1f - r.WellFedBasalReduction * wellFed)
         * (exposedAtNight ? r.NightOutsideDrainMul : 1f)
         * (1f + fever * r.FeverDrainMul)
         * (winter ? r.WinterDrainMul : 1f);

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

    public static void StepVirology(VirologyComponent virology, StatsComponent stats, MortalityComponent mortality,
        float dt, in HostState host)
    {
        float hpBefore = stats.Hp;
        virology.Step(stats, dt, in host);
        if (stats.Hp < hpBefore) mortality.NoteHarm(DeathCause.Virus, stats.Hp);
    }

    public static void Metabolize(StatsComponent stats, MortalityComponent mortality, float dt, float movementEnergy,
        float metabolismMultiplier, float basalMul, in SimulationRules.RulesFrame r, out float metabolismCredit)
    {
        float hpBefore = stats.Hp;
        stats.Tick(dt,
            movementEnergy,
            metabolismMultiplier,
            r.BasalEnergyDrain * basalMul,
            r.StarvationThreshold, r.StarvationDamage, r.StarvationEnergyReturn,
            out metabolismCredit);
        if (stats.Hp < hpBefore) mortality.NoteHarm(DeathCause.Starved, stats.Hp);
    }
}
