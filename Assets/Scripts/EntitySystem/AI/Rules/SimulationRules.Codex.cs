using System;
using UnityEngine;
using Unity.Mathematics;

public sealed partial class SimulationRules
{
    [SerializeField] private int codexMaxLaws = 32;
    [SerializeField] private int codexMaxAxesPerLaw = 3;
    [SerializeField] private float codexMaxDeltaPerAxis = 0.3f;
    [SerializeField] private float[] codexAxisMin = CodexFill(-0.5f);
    [SerializeField] private float[] codexAxisMax = CodexFill(0.5f);
    [SerializeField] private float codexVoteScale = 1f;
    [SerializeField] private float codexSupportDecay = 1f / 600f;
    [SerializeField] private float codexInertia = 6f;
    [SerializeField] private float codexPruneSupport = 0.05f;
    [SerializeField] private float codexFreeBudget = 0.02f;
    [SerializeField] private float codexProposeCost = 5f;
    [SerializeField] private float codexMinInvest = 1f;
    [SerializeField] private float[] codexPricePrior =
    {
        -0.5f, 0.3f, 0.4f, -0.1f, 0f, -0.05f, -0.1f,
        0f, -0.1f, -0.05f, 0f, -0.05f, 0.02f, 0f
    };
    [SerializeField] private float codexPriceForgetting = 0.98f;
    [SerializeField] private int codexPriceMinSamples = 24;
    [SerializeField] private float codexPriceMinExcitation = 1e-4f;
    [SerializeField] private float codexPriceInitCov = 10f;
    [SerializeField] private float codexWindow = 60f;
    [SerializeField] private float codexInvestFraction = 0.05f;
    [SerializeField] private float codexPerturbation = 0.1f;
    [SerializeField] private float codexMinColonyStore = 20f;
    [SerializeField] private float codexTrendEpsilon = 0.002f;

    public int CodexMaxLaws => codexMaxLaws;
    public int CodexMaxAxesPerLaw => codexMaxAxesPerLaw;
    public float CodexMaxDeltaPerAxis => codexMaxDeltaPerAxis;
    public ReadOnlySpan<float> CodexAxisMin => codexAxisMin;
    public ReadOnlySpan<float> CodexAxisMax => codexAxisMax;
    public float CodexVoteScale => codexVoteScale;
    public float CodexSupportDecay => codexSupportDecay;
    public float CodexInertia => codexInertia;
    public float CodexPruneSupport => codexPruneSupport;
    public float CodexFreeBudget => codexFreeBudget;
    public float CodexProposeCost => codexProposeCost;
    public float CodexMinInvest => codexMinInvest;
    public ReadOnlySpan<float> CodexPricePrior => codexPricePrior;
    public float CodexPriceForgetting => codexPriceForgetting;
    public int CodexPriceMinSamples => codexPriceMinSamples;
    public float CodexPriceMinExcitation => codexPriceMinExcitation;
    public float CodexPriceInitCov => codexPriceInitCov;
    public float CodexWindow => codexWindow;
    public float CodexInvestFraction => codexInvestFraction;
    public float CodexPerturbation => codexPerturbation;
    public float CodexMinColonyStore => codexMinColonyStore;
    public float CodexTrendEpsilon => codexTrendEpsilon;

    private static float[] CodexFill(float value)
    {
        var array = new float[(int)VarianceAxis.Count];
        for (int i = 0; i < array.Length; i++) array[i] = value;
        return array;
    }

    private static float[] CodexFit(float[] source, float fallback)
    {
        int count = (int)VarianceAxis.Count;
        if (source != null && source.Length == count) return source;

        var fit = new float[count];
        for (int i = 0; i < count; i++) fit[i] = source != null && i < source.Length ? source[i] : fallback;
        return fit;
    }

    private void ValidateCodex()
    {
        int count = (int)VarianceAxis.Count;
        codexAxisMin = CodexFit(codexAxisMin, -0.5f);
        codexAxisMax = CodexFit(codexAxisMax, 0.5f);
        codexPricePrior = CodexFit(codexPricePrior, 0f);

        for (int i = 0; i < count; i++)
        {
            codexAxisMin[i] = math.min(math.select(-0.5f, codexAxisMin[i], math.isfinite(codexAxisMin[i])), 0f);
            codexAxisMax[i] = math.max(math.select(0.5f, codexAxisMax[i], math.isfinite(codexAxisMax[i])), 0f);
            if (!math.isfinite(codexPricePrior[i])) codexPricePrior[i] = 0f;
        }

        codexMaxLaws = math.clamp(codexMaxLaws, 1, 256);
        codexMaxAxesPerLaw = math.clamp(codexMaxAxesPerLaw, 1, count);
        if (!(codexMaxDeltaPerAxis > 0f)) codexMaxDeltaPerAxis = 1e-3f;
        if (!(codexVoteScale > 0f)) codexVoteScale = 1e-3f;
        if (!(codexSupportDecay > 0f)) codexSupportDecay = 1e-6f;
        if (!(codexInertia > 0f)) codexInertia = 1e-3f;
        if (!(codexPruneSupport >= 0f)) codexPruneSupport = 0f;
        if (!math.isfinite(codexFreeBudget)) codexFreeBudget = 0f;
        if (!(codexProposeCost >= 0f)) codexProposeCost = 0f;
        if (!(codexMinInvest > 0f)) codexMinInvest = 1e-3f;
        codexPriceForgetting = math.clamp(codexPriceForgetting, 0.5f, 1f);
        if (codexPriceMinSamples < 1) codexPriceMinSamples = 1;
        if (!(codexPriceMinExcitation >= 0f)) codexPriceMinExcitation = 0f;
        if (!(codexPriceInitCov > 0f)) codexPriceInitCov = 1e-3f;
        if (!(codexWindow >= 1f)) codexWindow = 1f;
        codexInvestFraction = math.clamp(codexInvestFraction, 0f, 1f);
        codexPerturbation = math.clamp(codexPerturbation, 1e-3f, codexMaxDeltaPerAxis);
        if (!(codexMinColonyStore >= 0f)) codexMinColonyStore = 0f;
        if (!(codexTrendEpsilon >= 0f)) codexTrendEpsilon = 0f;
    }

    [NonSerialized] private SimulationRules lawBase;
    [NonSerialized] private float[] lawMultipliers;

    public SimulationRules CreateLawApplied()
    {
        var applied = Instantiate(this);
        applied.name = name + " (Laws)";
        applied.isVariant = true;
        applied.lawBase = this;
        applied.lawMultipliers = new float[(int)VarianceAxis.Count];
        return applied;
    }

    public void ApplyLaws(ReadOnlySpan<float> multipliers)
    {
        if (lawBase == null || multipliers.Length != (int)VarianceAxis.Count) return;

        CopyAxisFields(lawBase);
        bool identity = true;
        for (int i = 0; i < multipliers.Length; i++)
        {
            lawMultipliers[i] = multipliers[i];
            identity &= multipliers[i] == 1f;
        }
        if (!identity) ApplyVariance(lawMultipliers);
    }

    private void CopyAxisFields(SimulationRules s)
    {
        basalEnergyDrain = s.basalEnergyDrain;
        foodGrowthRate = s.foodGrowthRate;
        foodChunkCapacity = s.foodChunkCapacity;
        eatEnergyFood = s.eatEnergyFood;
        largeFoodEnergy = s.largeFoodEnergy;
        seasonAmplitude = s.seasonAmplitude;
        seasonPeriod = s.seasonPeriod;
        starvationDamage = s.starvationDamage;
        starvationThreshold = s.starvationThreshold;
        toxicDamage = s.toxicDamage;
        baseMutationRate = s.baseMutationRate;
        contactRadius = s.contactRadius;
        verticalTransmissionChance = s.verticalTransmissionChance;
        recombinationChance = s.recombinationChance;
        threatenRangeMul = s.threatenRangeMul;
        soldierAttackMul = s.soldierAttackMul;
        threatenEnergyCost = s.threatenEnergyCost;
        criticLearningRate = s.criticLearningRate;
        optimizerBaseLearningRate = s.optimizerBaseLearningRate;
        explorationEpsilonScale = s.explorationEpsilonScale;
        exploreTemperature = s.exploreTemperature;
        trailDeposit = s.trailDeposit;
        migrationTau = s.migrationTau;
        juvenileTime = s.juvenileTime;

        if (biomeToxicity == null || s.biomeToxicity == null) return;
        if (biomeToxicity.Length != s.biomeToxicity.Length) biomeToxicity = new float[s.biomeToxicity.Length];
        Array.Copy(s.biomeToxicity, biomeToxicity, biomeToxicity.Length);
    }
}
