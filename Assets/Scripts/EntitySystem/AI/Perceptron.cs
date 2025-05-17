using System;
using System.Collections.Generic;
using UnityEngine;

using ZLinq;
using R3;
using TheRavine.Extensions;
public partial class DelayedPerceptron
{
    private readonly float[][][] _weights;
    private readonly float[][]   _biases;
    private readonly float[][]   _activations;

    private readonly List<DelayedItem> _delayedList = new();
    private readonly int DelaySteps;

    private const float DefaultEvaluation = 0.5f, delta = 0.1f;
    private const float Lambda = 0.001f;

    public DelayedPerceptron(int inputSize, int hidden1, int hidden2, int outputSize,
                              int delaySteps = 5)
    {
        DelaySteps = delaySteps;

        var layerSizes = new[] { inputSize, hidden1, hidden2, outputSize };
        _weights     = new float[3][][];
        _biases      = new float[3][];
        _activations = new float[layerSizes.Length][];

        for (int i = 0; i < layerSizes.Length; i++)
            _activations[i] = new float[layerSizes[i]];

        for (int i = 0; i < 3; i++)
        {
            _weights[i] = InitWeights(layerSizes[i + 1], layerSizes[i]);
            _biases[i]  = InitBiases(layerSizes[i + 1]);
        }
    }

    public DelayedPerceptron(DelayedPerceptron other)
    {
        DelaySteps = other.DelaySteps;

        int[] layerSizes = other._activations.AsValueEnumerable().Select(a => a.Length).ToArray();
        _weights     = new float[3][][];
        _biases      = new float[3][];
        _activations = new float[layerSizes.Length][];

        for (int i = 0; i < layerSizes.Length; i++)
            _activations[i] = new float[layerSizes[i]];

        for (int layer = 0; layer < 3; layer++)
        {
            int neurons = layerSizes[layer + 1];
            int inputs  = layerSizes[layer];

            _weights[layer] = new float[neurons][];
            _biases[layer]  = new float[neurons];

            for (int n = 0; n < neurons; n++)
            {
                _weights[layer][n] = new float[inputs];

                if (layer < 2)
                {
                    Array.Copy(other._weights[layer][n], _weights[layer][n], inputs);
                    _biases[layer][n] = other._biases[layer][n];
                }
                else
                {
                    for (int i = 0; i < inputs; i++)
                    {
                        float noise = (RavineRandom.RangeFloat() * 2 - 1) * delta;
                        _weights[layer][n][i] = other._weights[layer][n][i] + noise;
                    }
                    _biases[layer][n] = other._biases[layer][n]
                                      + (RavineRandom.RangeFloat() * 2 - 1) * delta;
                }

                if(RavineRandom.Hundred() == 0) _weights[layer][n][RavineRandom.RangeInt(0, _weights[layer][n].Length)] += RavineRandom.RangeFloat();
            }
            if(RavineRandom.Hundred() == 0) _biases[layer][RavineRandom.RangeInt(0, _biases[layer].Length)] += RavineRandom.RangeFloat(0, 0.1f);
        }
    }

    public int Predict(float[] input, float epsilon = 0.1f)
    {
        ForwardPass(input);
        int pred;
        bool interest = false;
        
        if (RavineRandom.RangeFloat() < epsilon) 
        {
            // Случайное исследование
            pred = RavineRandom.RangeInt(0, _activations[3].Length);
            interest = true; 
        }
        else
        {
            // Эксплуатация: лучшее действие
            pred = ArgMax(_activations[3]); 
        }
        
        _delayedList.Add(new DelayedItem(input, pred));

        if(interest) _delayedList[_delayedList.Count - 1].Evaluation += 0.1f;
        

        if (_delayedList.Count > DelaySteps)
        {
            var item = _delayedList[0];
            _delayedList.RemoveAt(0);

            if (item.Evaluation > DefaultEvaluation)
                Train(item.Input, item.Predicted, item.Evaluation);
            else if (item.Evaluation < DefaultEvaluation)
                Train(item.Input, item.Predicted, 1f - item.Evaluation, isError: true);
        }

        return pred;
    }

    private void ForwardPass(float[] input)
    {
        Array.Copy(input, _activations[0], input.Length);
        for (int l = 0; l < 3; l++)
            ComputeLayer(l);
    }

    public void Train(float[] input, int predictedIndex, float reward, bool isError = false)
    {
        float lr = 0.01f * reward * (isError ? -1f : 1f);
        ForwardPass(input);

        int outCount = _activations[3].Length;
        var outputErrors = new float[outCount];

        float positiveTarget = 0.8f; // мягкая ошибка
        float negativeTarget = 0.2f / (outCount - 1);
        for (int i = 0; i < outCount; i++)
        {
            float target = (i == predictedIndex) ? positiveTarget : negativeTarget;
            if (isError && i == predictedIndex) target = -0.2f;
            outputErrors[i] = (target - _activations[3][i]) * LeakyReLUPrime(_activations[3][i]);
        }

        BackpropagateLayer(2, outputErrors, _activations[2]);
        var hidden2Errors = ComputeErrors(_weights[2], outputErrors, _activations[2]);
        BackpropagateLayer(1, hidden2Errors, _activations[1]);
    }  
    private void BackpropagateLayer(int layerIdx, float[] errors, float[] prevActivations)
    {
        if (layerIdx == 0) // т.к. первый слой должен доставаться от предков
            return;

        for (int i = 0; i < errors.Length; i++)
        {
            for (int j = 0; j < prevActivations.Length; j++)
            {
                _weights[layerIdx][i][j] += 0.01f * errors[i] * prevActivations[j] - Lambda * _weights[layerIdx][i][j];
            }
            _biases[layerIdx][i] += 0.01f * errors[i];
        }
    }

    private float[] ComputeErrors(float[][] nextWeights, float[] nextErrors, float[] activations)
    {
        var errors = new float[activations.Length];
        for (int i = 0; i < activations.Length; i++)
        {
            float sum = 0;
            for (int j = 0; j < nextErrors.Length; j++)
                sum += nextErrors[j] * nextWeights[j][i];
            errors[i] = sum * LeakyReLUPrime(activations[i]);
        }
        return errors;
    }

    private void ComputeLayer(int layer)
    {
        var input = _activations[layer];
        var output = _activations[layer + 1];

        for (int n = 0; n < output.Length; n++)
        {
            float sum = _biases[layer][n];
            for (int i = 0; i < input.Length; i++)
                sum += _weights[layer][n][i] * input[i];
            
            output[n] = (layer == 2) ? sum : LeakyReLU(sum);
        }

        if (layer == 2)
        {
            float[] softmaxOutput = Softmax(output);
            Array.Copy(softmaxOutput, output, output.Length);
        }
    }

    private static int ArgMax(float[] arr)
    {
        int best = 0;
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > arr[best]) best = i;
        return best;
    }

    private float[][] InitWeights(int neurons, int inputs)
    {
        var w = new float[neurons][];
        float scale = (float)Math.Sqrt(2.0 / (neurons + inputs));
        for (int i = 0; i < neurons; i++)
        {
            w[i] = new float[inputs];
            for (int j = 0; j < inputs; j++)
                w[i][j] = (RavineRandom.RangeFloat() * 2 - 1) * scale;
        }
        return w;
    }

    private float[] InitBiases(int neurons)
    {
        var b = new float[neurons];
        for (int i = 0; i < neurons; i++)
            b[i] = RavineRandom.RangeFloat() * 2 - 1;
        return b;
    }
    public static float LeakyReLU(float x, float alpha = 0.01f)
	{
		return x >= 0 ? x : (alpha * x);
	}

	public static float LeakyReLUPrime(float x, float alpha = 0.01f)
	{
		return x >= 0 ? 1 : alpha;
	}
    private static float[] Softmax(float[] layerOutputs)
    {
        float[] expValues = new float[layerOutputs.Length];
        float sumExp = 0f;

        float max = layerOutputs.AsValueEnumerable().Max();
        for (int i = 0; i < layerOutputs.Length; i++)
        {
            expValues[i] = (float)Math.Exp(layerOutputs[i] - max);
            sumExp += expValues[i];
        }

        for (int i = 0; i < expValues.Length; i++)
        {
            expValues[i] /= sumExp;
        }

        return expValues;
    }

    public List<DelayedItem> DelayedList => _delayedList; // доступ в внешней оценке
}

public class DelayedItem
{
    public float[] Input { get; }
    public int Predicted { get; }
    public float Evaluation { get; set; }

    public DelayedItem(float[] input, int pred)
    {
        Input = input;
        Predicted = pred;
        Evaluation = 0.5f;
    }
}