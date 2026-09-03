using UnityEngine;
using System;

public interface IGeneticPhenotype
{
    void ApplyGeneticPhenotype(GeneticParameters genetics);
}

[Serializable]
public struct GeneticParameters
{
    public float DefaultEvaluation;
    public float Lambda;
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float SoftmaxTemperature;
    public float EntropyRegularization;
    public float LabelSmoothing;
    public float EntropyAlpha;
    public float InitBiasesValues;
    public float GaussianNoise;
    public float ExplorationPrice;
    public float MutationChance;

    public float IdleLowEnergyThreshold;
    public float IdleLongActivityPenaltyStart;
    public float IdleRewardLowEnergy;
    public float IdleRewardOveractive;
    public float EatHealFood;
    public float EatEnergyFood;
    public float EatRewardFood;
    public float EatHealNoFood;
    public float EatEnergyNoFood;
    public float EatRewardNoFood;
    public float StarvationDamage;
    public float StarvationEnergyReturn;
    public float StarvationThreshold;
    public float RestHealRate;
    public float RestEnergyRate;
    public float RestDuration;
    public float RestDeficitThreshold;

    public static GeneticParameters Default => new()
    {
        DefaultEvaluation = RavineRandom.RangeFloat(ParameterRanges[0].min, ParameterRanges[0].max),
        Lambda = RavineRandom.RangeFloat(ParameterRanges[1].min, ParameterRanges[1].max),
        BaseLearningRate = RavineRandom.RangeFloat(ParameterRanges[2].min, ParameterRanges[2].max),
        MaxGradientNorm = RavineRandom.RangeFloat(ParameterRanges[3].min, ParameterRanges[3].max),
        SoftmaxTemperature = RavineRandom.RangeFloat(ParameterRanges[4].min, ParameterRanges[4].max),
        EntropyRegularization = RavineRandom.RangeFloat(ParameterRanges[5].min, ParameterRanges[5].max),
        LabelSmoothing = RavineRandom.RangeFloat(ParameterRanges[6].min, ParameterRanges[6].max),
        EntropyAlpha = RavineRandom.RangeFloat(ParameterRanges[7].min, ParameterRanges[7].max),
        InitBiasesValues = RavineRandom.RangeFloat(ParameterRanges[8].min, ParameterRanges[8].max),
        GaussianNoise = RavineRandom.RangeFloat(ParameterRanges[9].min, ParameterRanges[9].max),
        ExplorationPrice = RavineRandom.RangeFloat(ParameterRanges[10].min, ParameterRanges[10].max),
        MutationChance = RavineRandom.RangeFloat(ParameterRanges[11].min, ParameterRanges[11].max),
        IdleLowEnergyThreshold = RavineRandom.RangeFloat(ParameterRanges[12].min, ParameterRanges[12].max),
        IdleLongActivityPenaltyStart = RavineRandom.RangeFloat(ParameterRanges[13].min, ParameterRanges[13].max),
        IdleRewardLowEnergy = RavineRandom.RangeFloat(ParameterRanges[14].min, ParameterRanges[14].max),
        IdleRewardOveractive = RavineRandom.RangeFloat(ParameterRanges[15].min, ParameterRanges[15].max),
        EatHealFood = RavineRandom.RangeFloat(ParameterRanges[16].min, ParameterRanges[16].max),
        EatEnergyFood = RavineRandom.RangeFloat(ParameterRanges[17].min, ParameterRanges[17].max),
        EatRewardFood = RavineRandom.RangeFloat(ParameterRanges[18].min, ParameterRanges[18].max),
        EatHealNoFood = RavineRandom.RangeFloat(ParameterRanges[19].min, ParameterRanges[19].max),
        EatEnergyNoFood = RavineRandom.RangeFloat(ParameterRanges[20].min, ParameterRanges[20].max),
        EatRewardNoFood = RavineRandom.RangeFloat(ParameterRanges[21].min, ParameterRanges[21].max),
        StarvationDamage = RavineRandom.RangeFloat(ParameterRanges[22].min, ParameterRanges[22].max),
        StarvationEnergyReturn = RavineRandom.RangeFloat(ParameterRanges[23].min, ParameterRanges[23].max),
        StarvationThreshold = RavineRandom.RangeFloat(ParameterRanges[24].min, ParameterRanges[24].max),
        RestHealRate = RavineRandom.RangeFloat(ParameterRanges[25].min, ParameterRanges[25].max),
        RestEnergyRate = RavineRandom.RangeFloat(ParameterRanges[26].min, ParameterRanges[26].max),
        RestDuration = RavineRandom.RangeFloat(ParameterRanges[27].min, ParameterRanges[27].max),
        RestDeficitThreshold = RavineRandom.RangeFloat(ParameterRanges[28].min, ParameterRanges[28].max),
    };

    public static readonly (float min, float max, float mutationScale)[] ParameterRanges = {
        (0.1f, 0.9f, 0.1f),      // DefaultEvaluation
        (0.001f, 0.02f, 0.01f),  // Lambda
        (0.005f, 0.1f, 0.1f),    // BaseLearningRate
        (0.5f, 3.0f, 0.3f),      // MaxGradientNorm
        (0.8f, 3.0f, 0.3f),      // SoftmaxTemperature
        (0.01f, 0.2f, 0.1f),     // EntropyRegularization
        (0.1f, 0.5f, 0.1f),      // LabelSmoothing
        (0.05f, 0.3f, 0.1f),     // EntropyAlpha
        (0.01f, 0.3f, 0.1f),     // InitBiasesValues
        (0.01f, 0.1f, 0.05f),    // GaussianNoise
        (0.05f, 0.3f, 0.1f),     // ExplorationPrice
        (0.05f, 0.5f, 0.1f),     // MutationChance
        (0.2f, 0.5f, 0.05f),     // IdleLowEnergyThreshold
        (0.7f, 0.95f, 0.05f),    // IdleLongActivityPenaltyStart
        (0.3f, 0.9f, 0.1f),      // IdleRewardLowEnergy
        (-0.6f, -0.1f, 0.1f),    // IdleRewardOveractive
        (15f, 45f, 5f),          // EatHealFood
        (10f, 30f, 5f),          // EatEnergyFood
        (0.5f, 1f, 0.1f),        // EatRewardFood
        (1f, 10f, 2f),           // EatHealNoFood
        (1f, 10f, 2f),           // EatEnergyNoFood
        (0.1f, 0.6f, 0.1f),      // EatRewardNoFood
        (5f, 25f, 3f),           // StarvationDamage
        (1f, 10f, 2f),           // StarvationEnergyReturn
        (2f, 10f, 2f),           // StarvationThreshold
        (2f, 10f, 2f),           // RestHealRate
        (4f, 15f, 2f),           // RestEnergyRate
        (1f, 6f, 1f),            // RestDuration
        (0.1f, 0.5f, 0.1f),      // RestDeficitThreshold
    };

    public GeneticParameters GetMutatedGeneticParameters()
    {
        var paramArray = new float[]
        {
            DefaultEvaluation, Lambda, BaseLearningRate,
            MaxGradientNorm, SoftmaxTemperature, EntropyRegularization,
            LabelSmoothing, EntropyAlpha, InitBiasesValues,
            GaussianNoise, ExplorationPrice, MutationChance,
            IdleLowEnergyThreshold, IdleLongActivityPenaltyStart,
            IdleRewardLowEnergy, IdleRewardOveractive,
            EatHealFood, EatEnergyFood, EatRewardFood,
            EatHealNoFood, EatEnergyNoFood, EatRewardNoFood,
            StarvationDamage, StarvationEnergyReturn, StarvationThreshold,
            RestHealRate, RestEnergyRate, RestDuration, RestDeficitThreshold
        };
        for (int i = 0; i < paramArray.Length; i++)
        {
            if (RavineRandom.RangeFloat() < MutationChance)
            {
                var (min, max, scale) = ParameterRanges[i];
                float mutation = RavineRandom.RangeFloat(-1f, 1f) * scale;
                paramArray[i] = Mathf.Clamp(paramArray[i] + mutation, min, max);
            }
        }
        return new GeneticParameters
        {
            DefaultEvaluation = paramArray[0],
            Lambda = paramArray[1],
            BaseLearningRate = paramArray[2],
            MaxGradientNorm = paramArray[3],
            SoftmaxTemperature = paramArray[4],
            EntropyRegularization = paramArray[5],
            LabelSmoothing = paramArray[6],
            EntropyAlpha = paramArray[7],
            InitBiasesValues = paramArray[8],
            GaussianNoise = paramArray[9],
            ExplorationPrice = paramArray[10],
            MutationChance = paramArray[11],
            IdleLowEnergyThreshold = paramArray[12],
            IdleLongActivityPenaltyStart = paramArray[13],
            IdleRewardLowEnergy = paramArray[14],
            IdleRewardOveractive = paramArray[15],
            EatHealFood = paramArray[16],
            EatEnergyFood = paramArray[17],
            EatRewardFood = paramArray[18],
            EatHealNoFood = paramArray[19],
            EatEnergyNoFood = paramArray[20],
            EatRewardNoFood = paramArray[21],
            StarvationDamage = paramArray[22],
            StarvationEnergyReturn = paramArray[23],
            StarvationThreshold = paramArray[24],
            RestHealRate = paramArray[25],
            RestEnergyRate = paramArray[26],
            RestDuration = paramArray[27],
            RestDeficitThreshold = paramArray[28],
        };
    }

    public uint ComputeHash()
    {
        uint hash = 2166136261u;
        Hash(ref hash, DefaultEvaluation);
        Hash(ref hash, Lambda);
        Hash(ref hash, BaseLearningRate);
        Hash(ref hash, MaxGradientNorm);
        Hash(ref hash, SoftmaxTemperature);
        Hash(ref hash, EntropyRegularization);
        Hash(ref hash, LabelSmoothing);
        Hash(ref hash, EntropyAlpha);
        Hash(ref hash, InitBiasesValues);
        Hash(ref hash, GaussianNoise);
        Hash(ref hash, ExplorationPrice);
        Hash(ref hash, MutationChance);
        Hash(ref hash, IdleLowEnergyThreshold);
        Hash(ref hash, IdleLongActivityPenaltyStart);
        Hash(ref hash, IdleRewardLowEnergy);
        Hash(ref hash, IdleRewardOveractive);
        Hash(ref hash, EatHealFood);
        Hash(ref hash, EatEnergyFood);
        Hash(ref hash, EatRewardFood);
        Hash(ref hash, EatHealNoFood);
        Hash(ref hash, EatEnergyNoFood);
        Hash(ref hash, EatRewardNoFood);
        Hash(ref hash, StarvationDamage);
        Hash(ref hash, StarvationEnergyReturn);
        Hash(ref hash, StarvationThreshold);
        Hash(ref hash, RestHealRate);
        Hash(ref hash, RestEnergyRate);
        Hash(ref hash, RestDuration);
        Hash(ref hash, RestDeficitThreshold);
        return hash;
    }

    private static void Hash(ref uint hash, float value)
    {
        uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
        hash = (hash ^ bits) * 16777619u;
    }
}

public struct XorShift32
{
    private uint state;

    public XorShift32(uint seed) => state = seed == 0 ? 0xA5A5A5A5u : seed;

    public uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    public int Range(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
}