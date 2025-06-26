using System;
using System.Collections.Generic;

using UnityEngine;

using ZLinq;
using R3;
using TheRavine.Extensions;

public partial class DelayedPerceptron
{
    private float[][][] _weights;
    private float[][]   _biases;
    private float[][]   _activations;

    private readonly List<DelayedItem> _delayedList = new();
    private readonly int DelaySteps;

    private GeneticParameters _params;
    
    private int _trainingSteps = 0;
    private float _averageEntropy = 0f;
    
    private const float w1 = 0.7f, w2 = 0.2f, w3 = 0.1f;
    private static readonly float[] w = { w1, w2, w3 };
    private static readonly float[] layerStrength = { 0f, 2f, 5f, 10f, 30f };

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize, int delaySteps = 5)
    {
        DelaySteps = delaySteps;
        _params = GeneticParameters.Default;
        
        var layerSizes = new[] { inputSize, h1, h2, h3, outputSize };
        InitializeNetworkStructure(layerSizes);
    }

    // Конструктор генетического наследования
    public DelayedPerceptron(DelayedPerceptron parent)
    {
        DelaySteps = parent.DelaySteps;
        
        // Наследование параметров с мутацией
        _params = MutateParameters(parent._params);
        
        int[] layerSizes = parent._activations.AsValueEnumerable().Select(a => a.Length).ToArray();
        InitializeNetworkStructure(layerSizes);
        
        // Наследование весов и смещений с мутацией
        InheritWeightsAndBiases(parent, layerSizes);
    }

    private void InitializeNetworkStructure(int[] layerSizes)
    {
        _weights = new float[layerSizes.Length - 1][][];
        _biases = new float[layerSizes.Length - 1][];
        _activations = new float[layerSizes.Length][];

        for (int i = 0; i < layerSizes.Length; i++)
            _activations[i] = new float[layerSizes[i]];

        for (int L = 0; L < _weights.Length; L++)
        {
            _weights[L] = InitWeights(layerSizes[L + 1], layerSizes[L]);
            _biases[L] = InitBiases(layerSizes[L + 1]);
        }
    }

    private GeneticParameters MutateParameters(GeneticParameters parentParams)
    {
        var mutated = parentParams;
        var paramArray = new float[]
        {
            mutated.DefaultEvaluation, mutated.Lambda, mutated.BaseLearningRate,
            mutated.MaxGradientNorm, mutated.SoftmaxTemperature, mutated.EntropyRegularization,
            mutated.LabelSmoothing, mutated.EntropyAlpha, mutated.InitBiasesValues,
            mutated.GaussianNoise, mutated.ExplorationPrice, mutated.MutationChance, mutated.BaseDelta
        };

        // Мутация каждого параметра с вероятностью
        for (int i = 0; i < paramArray.Length; i++)
        {
            if (RavineRandom.RangeFloat() < parentParams.MutationChance)
            {
                var (min, max, scale) = GeneticParameters.ParameterRanges[i];
                float mutation = RavineRandom.RangeFloat(-1f, 1f) * scale;
                paramArray[i] = Mathf.Clamp(paramArray[i] + mutation, min, max);
            }
        }

        // Обновляем структуру
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

    private void InheritWeightsAndBiases(DelayedPerceptron parent, int[] layerSizes)
    {
        for (int L = 0; L < _weights.Length; L++)
        {
            int neurons = layerSizes[L + 1];
            int inputs = layerSizes[L];

            _weights[L] = new float[neurons][];
            _biases[L] = new float[neurons];

            float strength = layerStrength[L + 1];
            
            for (int n = 0; n < neurons; n++)
            {
                _weights[L][n] = new float[inputs];
                
                if (L <= 1) // Первые слои наследуются без мутации
                {
                    Array.Copy(parent._weights[L][n], _weights[L][n], inputs);
                    _biases[L][n] = parent._biases[L][n];
                }
                else // Глубокие слои мутируют
                {
                    for (int i = 0; i < inputs; i++)
                    {
                        _weights[L][n][i] = parent._weights[L][n][i];
                        
                        if (RavineRandom.RangeFloat() < _params.MutationChance)
                            _weights[L][n][i] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
                    }
                    
                    _biases[L][n] = parent._biases[L][n];
                    if (RavineRandom.RangeFloat() < _params.MutationChance)
                        _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
                }
            }
        }

        // Дополнительная глобальная мутация
        if (RavineRandom.RangeFloat() < _params.MutationChance)
        {
            ApplyGlobalMutation();
        }
    }

    private void ApplyGlobalMutation()
    {
        for (int L = 0; L < _weights.Length; L++)
        {
            float strength = layerStrength[L + 1];
            for (int n = 0; n < _weights[L].Length; n++)
            {
                if (RavineRandom.RangeFloat() < _params.MutationChance * 0.5f) // Меньшая вероятность для глобальной мутации
                {
                    for (int i = 0; i < _weights[L][n].Length; i++)
                        _weights[L][n][i] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength;
                }

                if (RavineRandom.RangeFloat() < _params.MutationChance * 0.5f)
                    _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * _params.BaseDelta * strength * 2f;
            }
        }
    }

    public int Predict(float[] input, float epsilon = 0.1f)
    {
        ForwardPass(input);

        int last = _activations.Length - 1;
        int pred;
        bool isExploration = false;
        float adaptiveEpsilon = epsilon * (1f + Math.Max(0f, 1.5f - _averageEntropy));

        if (RavineRandom.RangeFloat() < adaptiveEpsilon)
        {
            pred = RavineRandom.RangeInt(0, _activations[last].Length);
            isExploration = true;
        }
        else
        {
            pred = ArgMax(_activations[last]);
        }

        float currentEntropy = CalculateOutputEntropy(_activations[last]);
        _averageEntropy = _averageEntropy * (1f - _params.EntropyAlpha) + currentEntropy * _params.EntropyAlpha;

        var delayedItem = new DelayedItem(input, pred);
        if (isExploration)
            delayedItem.Evaluation += _params.ExplorationPrice;

        _delayedList.Add(delayedItem);

        if (_delayedList.Count > DelaySteps)
        {
            var item = _delayedList[0];
            _delayedList.RemoveAt(0);

            const float threshold = 0.05f;
            if (Math.Abs(item.Evaluation - _params.DefaultEvaluation) > threshold)
            {
                if (item.Evaluation > _params.DefaultEvaluation)
                    Train(item.Input, item.Predicted, item.Evaluation);
                else
                    Train(item.Input, item.Predicted, 1f - item.Evaluation);
            }
        }

        return pred;
    }

    public void Train(float[] input, int predictedIndex, float reward)
    {
        _trainingSteps++;
        
        float learningRateSchedule = GetLearningRateSchedule();
        float entropyBoost = Math.Min(2f, 1f + (1.5f - _averageEntropy) * 0.3f);
        float lr = _params.BaseLearningRate * Math.Min(reward, 1.2f) * learningRateSchedule * entropyBoost;

        // Добавление шума к входным данным
        float[] noisedInput = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            noisedInput[i] = input[i] + RavineRandom.RangeFloat(-_params.GaussianNoise, _params.GaussianNoise);
        }

        ForwardPass(noisedInput);

        int last = _activations.Length - 1;
        int outCount = _activations[last].Length;
        var outputErrors = new float[outCount];

        float positiveTarget = 1f - _params.LabelSmoothing;
        float negativeTarget = _params.LabelSmoothing / (outCount - 1);

        // Вычисление ошибок выходного слоя
        for (int i = 0; i < outCount; i++)
        {
            float target = (i == predictedIndex) ? positiveTarget : negativeTarget;
            float activation = _activations[last][i];
            
            float mainError = (target - activation) / (activation * (1f - activation) + 1e-8f);
            float entropyPenalty = _params.EntropyRegularization * (0.5f - activation);
            
            outputErrors[i] = mainError + entropyPenalty;
        }

        // Обратное распространение
        for (int layer = _weights.Length - 1; layer >= 1; layer--)
        {
            float[] errors = (layer == _weights.Length - 1) ? 
                outputErrors : 
                ComputeErrors(_weights[layer], outputErrors, _activations[layer]);

            float[] prevActs = _activations[layer];
            BackpropagateLayer(layer, errors, prevActs, lr);
            outputErrors = errors;
        }
    }

    private void BackpropagateLayer(int layerIdx, float[] errors, float[] prevActivations, float lr)
    {
        for (int i = 0; i < errors.Length; i++)
        {
            for (int j = 0; j < prevActivations.Length; j++)
            {
                float gradient = lr * errors[i] * prevActivations[j];
                gradient = Mathf.Clamp(gradient, -_params.MaxGradientNorm, _params.MaxGradientNorm);
                float adaptiveLambda = _params.Lambda * (2.0f - _averageEntropy);
                
                _weights[layerIdx][i][j] += gradient - adaptiveLambda * _weights[layerIdx][i][j];
            }
            
            float biasGradient = Mathf.Clamp(lr * errors[i], -_params.MaxGradientNorm, _params.MaxGradientNorm);
            _biases[layerIdx][i] += biasGradient;
        }
    }

    private float[] ComputeErrors(float[][] nextWeights, float[] nextErrors, float[] activations)
    {
        var errors = new float[activations.Length];
        for (int i = 0; i < activations.Length; i++)
        {
            float sum = 0f;
            for (int j = 0; j < nextErrors.Length; j++)
                sum += nextErrors[j] * nextWeights[j][i];
            errors[i] = sum * LeakyReLUPrime(activations[i]);
        }
        return errors;
    }

    private void ForwardPass(float[] input)
    {
        Array.Copy(input, _activations[0], input.Length);
        for (int l = 0; l < _weights.Length; l++)
            ComputeLayer(l);
    }

    private void ComputeLayer(int layer)
    {
        var input  = _activations[layer];
        var output = _activations[layer + 1];

        for (int n = 0; n < output.Length; n++)
        {
            float sum = _biases[layer][n];
            for (int i = 0; i < input.Length; i++)
                sum += _weights[layer][n][i] * input[i];
            output[n] = (layer == _weights.Length - 1) ? sum : LeakyReLU(sum);
        }
        
        if (layer == _weights.Length - 1)
        {
            var soft = SoftmaxWithTemperature(output, _params.SoftmaxTemperature);
            Array.Copy(soft, output, output.Length);
        }
    }

    private float GetLearningRateSchedule()
    {
        return (float)Math.Exp(-_trainingSteps * 0.0002f);
    }

    private float CalculateOutputEntropy(float[] outputs)
    {
        float entropy = 0f;
        for (int i = 0; i < outputs.Length; i++)
        {
            if (outputs[i] > 1e-8f)
                entropy -= outputs[i] * (float)Math.Log(outputs[i]);
        }
        return entropy;
    }

    private static float[] SoftmaxWithTemperature(float[] layerOutputs, float temperature)
    {
        float[] expValues = new float[layerOutputs.Length];
        float sumExp = 0f;

        float max = layerOutputs.AsValueEnumerable().Max();
        for (int i = 0; i < layerOutputs.Length; i++)
        {
            expValues[i] = (float)Math.Exp((layerOutputs[i] - max) / temperature);
            sumExp += expValues[i];
        }

        for (int i = 0; i < expValues.Length; i++)
        {
            expValues[i] /= sumExp;
        }

        return expValues;
    }

    private int ArgMax(float[] arr)
    {
        var pairs = arr
            .AsValueEnumerable()
            .Select((v, i) => (value: v, idx: i))
            .OrderByDescending(p => p.value)
            .Take(3)
            .ToArray();

        float pick = RavineRandom.RangeFloat();
        float cum = 0f;
        for (int k = 0; k < 3; k++)
        {
            cum += w[k];
            if (pick <= cum)
                return pairs[k].idx;
        }

        return pairs[0].idx;
    }

    private float[][] InitWeights(int neurons, int inputs)
    {
        var weights = new float[neurons][];
        float scale = (float)Math.Sqrt(2.0 / (neurons + inputs));
        
        for (int i = 0; i < neurons; i++)
        {
            weights[i] = new float[inputs];
            for (int j = 0; j < inputs; j++)
            {
                float u1 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                float u2 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                float gaussian = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
                weights[i][j] = gaussian * scale;
            }
        }
        return weights;
    }

    private float[] InitBiases(int neurons)
    {
        var biases = new float[neurons];
        for (int i = 0; i < neurons; i++)
            biases[i] = RavineRandom.RangeFloat(-_params.InitBiasesValues, _params.InitBiasesValues);
        return biases;
    }

    public static float LeakyReLU(float x, float alpha = 0.01f)
    {
        return x >= 0 ? x : (alpha * x);
    }

    public static float LeakyReLUPrime(float x, float alpha = 0.01f)
    {
        return x >= 0 ? 1 : alpha;
    }

    // Методы для получения генетической информации
    public GeneticParameters GetGeneticParameters() => _params;
    public float GetFitness() => _averageEntropy + (1f / Math.Max(_trainingSteps, 1));
    
    // Публичные свойства
    public float AverageEntropy => _averageEntropy;
    public int TrainingSteps => _trainingSteps;
    public List<DelayedItem> DelayedList => _delayedList;
    
    public void ResetTrainingStats()
    {
        _trainingSteps = 0;
        _averageEntropy = 0f;
    }
}