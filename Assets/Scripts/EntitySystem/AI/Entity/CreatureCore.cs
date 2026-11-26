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

    public const int StressCalm = 0;
    public const int StressStressed = 1;
    public const int StressHigh = 2;

    public static float HungerStressRate(float energyFraction, in SimulationRules.RulesFrame r)
    {
        if (!r.StressSourcesEnabled || r.StressHungerThreshold <= 0f) return 0f;
        return r.StressHungerRate * math.saturate(1f - energyFraction / r.StressHungerThreshold);
    }

    public static float ColdStressRate(float environmentDrainMul, bool exposed, in SimulationRules.RulesFrame r)
        => r.StressSourcesEnabled && exposed ? r.StressColdRate * math.max(0f, environmentDrainMul - 1f) : 0f;

    public static float LonelyStressRate(float aloneSeconds, in SimulationRules.RulesFrame r)
        => r.StressSourcesEnabled && aloneSeconds > r.LonelinessWindow ? r.StressLonelyRate : 0f;

    public static float StepAloneSeconds(float aloneSeconds, bool hasKin, float dt)
        => hasKin ? 0f : aloneSeconds + dt;

    public static float SmoothStress(float smooth, float raw, float dt, in SimulationRules.RulesFrame r)
    {
        if (r.StressSmoothTau <= 0f) return raw;
        return smooth + (raw - smooth) * (1f - math.exp(-dt / r.StressSmoothTau));
    }

    public static int NextStressLevel(int level, float smooth, in SimulationRules.RulesFrame r)
    {
        if (level >= StressHigh)
        {
            if (smooth >= r.StressHighExit) return StressHigh;
            level = StressStressed;
        }
        else if (level == StressStressed && smooth >= r.StressHighEnter) return StressHigh;

        if (level == StressStressed) return smooth >= r.StressStressedExit ? StressStressed : StressCalm;
        return smooth >= r.StressStressedEnter ? (smooth >= r.StressHighEnter ? StressHigh : StressStressed) : StressCalm;
    }

    public static float StressEffect(float smooth, int level, in SimulationRules.RulesFrame r)
        => r.StressSourcesEnabled && level > StressCalm ? math.saturate(smooth) : 0f;

    public static float StressDrainMul(float effect, in SimulationRules.RulesFrame r)
        => 1f + r.StressDrainMul * effect;

    public static float StressRadiusMul(float effect, in SimulationRules.RulesFrame r)
        => math.max(1f - r.StressRadiusShrink * effect, 0.05f);

    public static float StressDiscountPower(float effect, in SimulationRules.RulesFrame r)
        => 1f + r.StressDiscountMul * effect;

    public static float StressLearningScale(float effect, in SimulationRules.RulesFrame r)
        => math.max(r.StressLrFloor, 1f - r.StressLrMul * effect);

    public static float StressAbandonChance(float effect, in SimulationRules.RulesFrame r)
        => r.StressAbandonChance * effect;

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
