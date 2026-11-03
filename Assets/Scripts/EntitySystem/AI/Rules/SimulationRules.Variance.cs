using System;
using UnityEngine;
using Unity.Mathematics;

public enum VarianceAxis
{
    Metabolism,
    FoodGrowth,
    FoodValue,
    SeasonAmplitude,
    SeasonPeriod,
    Starvation,
    Toxicity,
    Mutation,
    VirusSpread,
    Aggression,
    LearningRate,
    Exploration,
    Pheromone,
    Juvenile,
    Count
}

public sealed partial class SimulationRules
{
    [SerializeField] private float[] varianceSpread =
    {
        0.25f, 0.5f, 0.3f, 0.3f, 0.5f, 0.25f, 0.5f,
        0.5f, 0.25f, 0.5f, 0.5f, 0.3f, 0.3f, 0.25f
    };

    [NonSerialized] private bool isVariant;

    public bool IsVariant => isVariant;

    private float SpreadOf(VarianceAxis axis)
    {
        if (varianceSpread == null || varianceSpread.Length != (int)VarianceAxis.Count) return 0f;
        return math.max(0f, varianceSpread[(int)axis]);
    }

    public float[] RollVariance(int seed)
    {
        var multipliers = new float[(int)VarianceAxis.Count];
        var rng = new FastRandom((uint)seed);
        rng.GetInt();
        rng.GetInt();
        rng.GetInt();
        rng.GetInt();

        for (int i = 0; i < multipliers.Length; i++)
        {
            float spread = SpreadOf((VarianceAxis)i);
            if (spread <= 0f)
            {
                multipliers[i] = 1f;
                continue;
            }

            float bound = math.log(1f + spread);
            multipliers[i] = math.exp(rng.Range(-bound, bound));
        }

        return multipliers;
    }

    public SimulationRules CreateVariant(float[] multipliers)
    {
        if (multipliers == null || multipliers.Length != (int)VarianceAxis.Count) return this;

        var variant = Instantiate(this);
        variant.name = name + " (Variant)";
        variant.isVariant = true;
        variant.ApplyVariance(multipliers);
        return variant;
    }

    public static void ReleaseVariant()
    {
        var active = _active;
        if (active == null || !active.isVariant) return;

        _active = null;
        Destroy(active);
    }

    private void ApplyVariance(float[] m)
    {
        basalEnergyDrain = math.max(0f, basalEnergyDrain * m[(int)VarianceAxis.Metabolism]);

        float foodGrowth = m[(int)VarianceAxis.FoodGrowth];
        foodGrowthRate = math.max(0f, foodGrowthRate * foodGrowth);
        foodChunkCapacity = math.max(0f, foodChunkCapacity * foodGrowth);

        float foodValue = m[(int)VarianceAxis.FoodValue];
        eatEnergyFood = math.max(0f, eatEnergyFood * foodValue);
        largeFoodEnergy = math.max(0f, largeFoodEnergy * foodValue);

        seasonAmplitude = math.clamp(seasonAmplitude * m[(int)VarianceAxis.SeasonAmplitude], 0f, 0.9f);
        seasonPeriod = math.max(60f, seasonPeriod * m[(int)VarianceAxis.SeasonPeriod]);

        float starvation = m[(int)VarianceAxis.Starvation];
        starvationDamage = math.max(0f, starvationDamage * starvation);
        starvationThreshold = math.max(0f, starvationThreshold * starvation);

        float toxicity = m[(int)VarianceAxis.Toxicity];
        toxicDamage = math.max(0f, toxicDamage * toxicity);
        if (biomeToxicity != null)
        {
            for (int i = 0; i < biomeToxicity.Length; i++)
                biomeToxicity[i] = math.clamp(biomeToxicity[i] * toxicity, 0f, 1f);
        }

        baseMutationRate = math.clamp(baseMutationRate * m[(int)VarianceAxis.Mutation], mutationRateMin, mutationRateMax);

        float virus = m[(int)VarianceAxis.VirusSpread];
        contactRadius = math.max(0f, contactRadius * virus);
        verticalTransmissionChance = math.clamp(verticalTransmissionChance * virus, 0f, 1f);
        recombinationChance = math.clamp(recombinationChance * virus, 0f, 1f);

        float aggression = m[(int)VarianceAxis.Aggression];
        threatenRangeMul = math.max(0f, threatenRangeMul * aggression);
        soldierAttackMul = math.max(0f, soldierAttackMul * aggression);
        threatenEnergyCost = math.max(0f, threatenEnergyCost * aggression);

        float learning = m[(int)VarianceAxis.LearningRate];
        criticLearningRate = math.max(0f, criticLearningRate * learning);
        optimizerBaseLearningRate = math.max(LearningRateFloor, optimizerBaseLearningRate * learning);

        float exploration = m[(int)VarianceAxis.Exploration];
        explorationEpsilonScale = math.max(0f, explorationEpsilonScale * exploration);
        exploreTemperature = math.max(0f, exploreTemperature * exploration);

        float pheromone = m[(int)VarianceAxis.Pheromone];
        trailDeposit = math.max(0f, trailDeposit * pheromone);
        migrationTau = math.max(0f, migrationTau * pheromone);

        juvenileTime = math.max(10f, juvenileTime * m[(int)VarianceAxis.Juvenile]);
    }
}
