using System;
using System.Collections.Generic;

using UnityEngine;

using ZLinq;
using R3;
using TheRavine.Extensions;

public partial class DelayedPerceptron
{
    public DelayedPerceptron(DelayedPerceptron parentA, DelayedPerceptron parentB)
    {
        DelaySteps = RavineRandom.RangeBool() ? parentA.DelaySteps : parentB.DelaySteps;
        
        _params = CrossoverAndMutateParameters(parentA._params, parentB._params);
        
        int[] layerSizes = parentA._activations
            .AsValueEnumerable()
            .Select(a => a.Length)
            .ToArray();
        
        InitializeNetworkStructure(layerSizes);
        
        CrossoverAndMutateWeightsAndBiases_Optimized(parentA, parentB, layerSizes);
    }

    private GeneticParameters CrossoverAndMutateParameters(GeneticParameters a, GeneticParameters b)
    {
        var result = new GeneticParameters
        {
            DefaultEvaluation = RavineRandom.RangeBool() ? a.DefaultEvaluation : b.DefaultEvaluation,
            Lambda = RavineRandom.RangeBool() ? a.Lambda : b.Lambda,
            BaseLearningRate = RavineRandom.RangeBool() ? a.BaseLearningRate : b.BaseLearningRate,
            MaxGradientNorm = RavineRandom.RangeBool() ? a.MaxGradientNorm : b.MaxGradientNorm,
            SoftmaxTemperature = RavineRandom.RangeBool() ? a.SoftmaxTemperature : b.SoftmaxTemperature,
            EntropyRegularization = RavineRandom.RangeBool() ? a.EntropyRegularization : b.EntropyRegularization,
            LabelSmoothing = RavineRandom.RangeBool() ? a.LabelSmoothing : b.LabelSmoothing,
            EntropyAlpha = RavineRandom.RangeBool() ? a.EntropyAlpha : b.EntropyAlpha,
            InitBiasesValues = RavineRandom.RangeBool() ? a.InitBiasesValues : b.InitBiasesValues,
            GaussianNoise = RavineRandom.RangeBool() ? a.GaussianNoise : b.GaussianNoise,
            ExplorationPrice = RavineRandom.RangeBool() ? a.ExplorationPrice : b.ExplorationPrice,
            MutationChance = RavineRandom.RangeBool() ? a.MutationChance : b.MutationChance,
            BaseDelta = RavineRandom.RangeBool() ? a.BaseDelta : b.BaseDelta
        };
        
        var paramArray = new float[]
        {
            result.DefaultEvaluation, result.Lambda, result.BaseLearningRate,
            result.MaxGradientNorm, result.SoftmaxTemperature, result.EntropyRegularization,
            result.LabelSmoothing, result.EntropyAlpha, result.InitBiasesValues,
            result.GaussianNoise, result.ExplorationPrice, result.MutationChance, result.BaseDelta
        };
        for (int i = 0; i < paramArray.Length; i++)
        {
            if (RavineRandom.RangeFloat() < result.MutationChance)
            {
                var (min, max, scale) = GeneticParameters.ParameterRanges[i];
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
            BaseDelta = paramArray[12]
        };
    }

    private void CrossoverAndMutateWeightsAndBiases_Optimized(DelayedPerceptron A, DelayedPerceptron B, int[] layerSizes)
    {
        for (int L = 0; L < _weights.Length; L++)
        {
            int neurons = layerSizes[L + 1];
            int inputs = layerSizes[L];
            float strength = layerStrength[L + 1];
            
            _weights[L] = new float[neurons][];
            _biases[L] = new float[neurons];
            
            CrossoverStrategy strategy = GetCrossoverStrategy(L, _weights.Length);
            
            for (int n = 0; n < neurons; n++)
            {
                _weights[L][n] = new float[inputs];
                
                switch (strategy)
                {
                    case CrossoverStrategy.Uniform:
                        ApplyUniformCrossover(A, B, L, n, inputs, strength);
                        break;
                        
                    case CrossoverStrategy.SinglePoint:
                        ApplySinglePointCrossover(A, B, L, n, inputs, strength);
                        break;
                        
                    case CrossoverStrategy.Arithmetic:
                        ApplyArithmeticCrossover(A, B, L, n, inputs, strength);
                        break;
                }
            }
        }
    }

    private enum CrossoverStrategy
    {
        Uniform,      // Равномерный кроссовер
        SinglePoint,  // Одноточечный кроссовер
        Arithmetic    // Арифметический кроссовер (усреднение)
    }

    private CrossoverStrategy GetCrossoverStrategy(int layer, int totalLayers)
    {
        // Ранние слои - более консервативный кроссовер
        if (layer < totalLayers / 2)
            return CrossoverStrategy.Arithmetic;
        else
            return RavineRandom.RangeFloat() < 0.5f ? CrossoverStrategy.Uniform : CrossoverStrategy.SinglePoint;
    }

    private void ApplyUniformCrossover(DelayedPerceptron A, DelayedPerceptron B, int L, int n, int inputs, float strength)
    {
        _biases[L][n] = RavineRandom.RangeBool() ? A._biases[L][n] : B._biases[L][n];
        
        for (int i = 0; i < inputs; i++)
        {
            _weights[L][n][i] = RavineRandom.RangeBool() ? A._weights[L][n][i] : B._weights[L][n][i];
            
            // Мутация
            if (RavineRandom.RangeFloat() < _params.MutationChance)
                _weights[L][n][i] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
        }
        
        if (RavineRandom.RangeFloat() < _params.MutationChance)
            _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
    }

    private void ApplySinglePointCrossover(DelayedPerceptron A, DelayedPerceptron B, int L, int n, int inputs, float strength)
    {
        int crossoverPoint = RavineRandom.RangeInt(0, inputs);
        _biases[L][n] = RavineRandom.RangeBool() ? A._biases[L][n] : B._biases[L][n];
        
        for (int i = 0; i < inputs; i++)
        {
            _weights[L][n][i] = (i < crossoverPoint) ? A._weights[L][n][i] : B._weights[L][n][i];
            
            // Мутация
            if (RavineRandom.RangeFloat() < _params.MutationChance)
                _weights[L][n][i] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
        }
        
        if (RavineRandom.RangeFloat() < _params.MutationChance)
            _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
    }

    private void ApplyArithmeticCrossover(DelayedPerceptron A, DelayedPerceptron B, int L, int n, int inputs, float strength)
    {
        float alpha = RavineRandom.RangeFloat(0.3f, 0.7f); // Коэффициент смешивания
        
        _biases[L][n] = alpha * A._biases[L][n] + (1f - alpha) * B._biases[L][n];
        
        for (int i = 0; i < inputs; i++)
        {
            _weights[L][n][i] = alpha * A._weights[L][n][i] + (1f - alpha) * B._weights[L][n][i];
            
            // Мутация
            if (RavineRandom.RangeFloat() < _params.MutationChance)
                _weights[L][n][i] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
        }
        
        if (RavineRandom.RangeFloat() < _params.MutationChance)
            _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
    }
}