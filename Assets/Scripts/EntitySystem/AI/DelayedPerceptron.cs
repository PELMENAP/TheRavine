using System;
using System.Collections.Generic;

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

    private const float DefaultEvaluation = 0.5f;
    private const float Lambda = 0.001f;

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize, int delaySteps = 5)
    {
        DelaySteps = delaySteps;
        var layerSizes = new[] { inputSize, h1, h2, h3, outputSize };
        _weights     = new float[layerSizes.Length - 1][][];
        _biases      = new float[layerSizes.Length - 1][];
        _activations = new float[layerSizes.Length][];

        for (int i = 0; i < layerSizes.Length; i++)
            _activations[i] = new float[layerSizes[i]];

        for (int L = 0; L < _weights.Length; L++)
        {
            _weights[L] = InitWeights(layerSizes[L + 1], layerSizes[L]);
            _biases[L]  = InitBiases(layerSizes[L + 1]);
        }
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        DelaySteps = parent.DelaySteps;
        int[] layerSizes = parent._activations.AsValueEnumerable().Select(a => a.Length).ToArray();
        _weights     = new float[layerSizes.Length - 1][][];
        _biases      = new float[layerSizes.Length - 1][];
        _activations = new float[layerSizes.Length][];

        for (int i = 0; i < layerSizes.Length; i++)
            _activations[i] = new float[layerSizes[i]];

        const float baseDelta = 0.05f;
        float[] layerStrength = { 0f, 2f, 5f, 10f, 30f }; 

        for (int L = 0; L < _weights.Length; L++)
        {
            int neurons = layerSizes[L + 1];
            int inputs  = layerSizes[L];

            _weights[L] = new float[neurons][];
            _biases[L]  = new float[neurons];

            float strength = layerStrength[L + 1]; 
            for (int n = 0; n < neurons; n++)
            {
                _weights[L][n] = new float[inputs];
                if (L <= 1)
                {
                    Array.Copy(parent._weights[L][n], _weights[L][n], inputs);
                    _biases[L][n] = parent._biases[L][n];
                }
                else
                {
                    for (int i = 0; i < inputs; i++)
                    {
                        float noise = RavineRandom.RangeFloat(-1f, 1f) * baseDelta * strength;
                        _weights[L][n][i] = parent._weights[L][n][i] + noise;
                    }
                    _biases[L][n] = parent._biases[L][n] + RavineRandom.RangeFloat(-1f, 1f) * baseDelta * strength;
                }
            }
        }

        // 10% шанс на глобальную мутацию всех слоёв
        if (RavineRandom.Hundred() < 10)
        {
            for (int L = 0; L < _weights.Length; L++)
            {
                float strength = layerStrength[L + 1];
                for (int n = 0; n < _weights[L].Length; n++)
                {
                    for (int i = 0; i < _weights[L][n].Length; i++)
                    {
                        float noise = RavineRandom.RangeFloat(-1f, 1f) * baseDelta * strength * 2f;
                        _weights[L][n][i] += noise;
                    }
                    _biases[L][n] += RavineRandom.RangeFloat(-1f, 1f) * baseDelta * strength * 2f;
                }
            }
        }
    }
    public int Predict(float[] input, float epsilon = 0.1f)
    {
        ForwardPass(input);

        int last = _activations.Length - 1;
        int pred;
        bool interest = false;

        if (RavineRandom.RangeFloat() < epsilon)
        {
            pred = RavineRandom.RangeInt(0, _activations[last].Length);
            interest = true;
        }
        else
        {
            pred = ArgMax(_activations[last]);
        }

        _delayedList.Add(new DelayedItem(input, pred));
        if (interest)
            _delayedList[^1].Evaluation += 0.1f;

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
        for (int l = 0; l < _weights.Length; l++)
            ComputeLayer(l);
    }

    public void Train(float[] input, int predictedIndex, float reward, bool isError = false)
    {
        float lrSign = isError ? -1f : 1f;
        float lr = 0.01f * reward * lrSign;

        ForwardPass(input);

        int last = _activations.Length - 1;
        int outCount = _activations[last].Length;
        var outputErrors = new float[outCount];

        float positiveTarget = 0.8f;
        float negativeTarget = 0.2f / (outCount - 1);
        for (int i = 0; i < outCount; i++)
        {
            float target = (i == predictedIndex) ? positiveTarget : negativeTarget;
            if (isError && i == predictedIndex)
                target = -0.2f;

            float activation = _activations[last][i];
            outputErrors[i] = target - activation;
        }

        for (int layer = _weights.Length - 1; layer >= 1; layer--)
        {
            float[] errors = (layer == _weights.Length - 1) ? outputErrors : ComputeErrors(_weights[layer], outputErrors, _activations[layer]);

            float[] prevActs = _activations[layer];
            BackpropagateLayer(layer, errors, prevActs, lr);
            outputErrors = errors;
        }
    }

    private void BackpropagateLayer(int layerIdx, float[] errors, float[] prevActivations, float lr)
    {
        if (layerIdx == 0) return;
        for (int i = 0; i < errors.Length; i++)
        {
            for (int j = 0; j < prevActivations.Length; j++)
            {
                _weights[layerIdx][i][j] += lr * errors[i] * prevActivations[j]
                                            - Lambda * _weights[layerIdx][i][j];
            }
            _biases[layerIdx][i] += lr * errors[i];
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
            var soft = Softmax(output);
            Array.Copy(soft, output, output.Length);
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
                w[i][j] = RavineRandom.RangeFloat(-1f, 1f) * scale;
        }
        return w;
    }

    private float[] InitBiases(int neurons)
    {
        var b = new float[neurons];
        for (int i = 0; i < neurons; i++)
            b[i] = RavineRandom.RangeFloat(-1f, 1f);
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

    public List<DelayedItem> DelayedList => _delayedList;
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