using TheRavine.Extensions;
using UnityEngine;

[System.Serializable]
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
    public float MutationStrength;
    public float BaseDelta;

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
        MutationStrength = RavineRandom.RangeFloat(ParameterRanges[12].min, ParameterRanges[12].max),
        BaseDelta = RavineRandom.RangeFloat(ParameterRanges[13].min, ParameterRanges[13].max),
    };
    public static readonly (float min, float max, float mutationScale)[] ParameterRanges = {
        (0.1f, 0.9f, 0.1f),     // DefaultEvaluation
        (0.001f, 0.02f, 0.01f), // Lambda
        (0.005f, 0.1f, 0.1f),   // BaseLearningRate
        (0.5f, 3.0f, 0.3f),      // MaxGradientNorm
        (0.8f, 3.0f, 0.3f),      // SoftmaxTemperature
        (0.01f, 0.2f, 0.1f),    // EntropyRegularization
        (0.1f, 0.5f, 0.1f),     // LabelSmoothing
        (0.05f, 0.3f, 0.1f),    // EntropyAlpha
        (0.01f, 0.3f, 0.1f),    // InitBiasesValues
        (0.01f, 0.1f, 0.05f),    // GaussianNoise
        (0.05f, 0.3f, 0.1f),    // ExplorationPrice
        (0.05f, 0.5f, 0.1f),    // MutationChance
        (0.05f, 1f, 0.1f),    // MutationStrength
        (0.01f, 0.1f, 0.05f)     // BaseDeltan    
    };

    public GeneticParameters GetMutatedGeneticParameters()
    {
        var paramArray = new float[]
        {
            DefaultEvaluation, Lambda, BaseLearningRate,
            MaxGradientNorm, SoftmaxTemperature, EntropyRegularization,
            LabelSmoothing, EntropyAlpha, InitBiasesValues,
            GaussianNoise, ExplorationPrice, MutationChance, MutationStrength, BaseDelta
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
            MutationStrength = paramArray[12],
            BaseDelta = paramArray[13],
        };
    }
}