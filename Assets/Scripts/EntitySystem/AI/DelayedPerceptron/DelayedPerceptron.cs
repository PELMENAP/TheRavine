using System;
using UnityEngine;
using ZLinq;
using TheRavine.Extensions;

public partial class DelayedPerceptron
{
    private float[][][] _weights;
    private float[][]   _biases;

    private static readonly float[] w             = { 0.7f, 0.2f, 0.1f };
    private static readonly float[] layerStrength = { 0f, 2f, 5f, 10f, 30f };

    public int[] LayerSizes { get; private set; }

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize)
    {
        LayerSizes = new[] { inputSize, h1, h2, h3, outputSize };
        InitWeightsAndBiases(LayerSizes);
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        LayerSizes = parent.LayerSizes;
        CloneWeights(parent);
        MutateWeights(parent._biases[0].Length > 0
            ? parent.MakeTempContext().Params
            : GeneticParameters.Default);
    }

    public PerceptronContext CreateContext(GeneticParameters? p = null)
        => new PerceptronContext(LayerSizes, p ?? GeneticParameters.Default);

    // Temp context используется только внутри ctor мутации — не хранить
    private PerceptronContext MakeTempContext() => CreateContext();


    public int Predict(float[] input, PerceptronContext ctx, int delaySteps, float epsilon = 0.1f)
    {
        ForwardPass(input, ctx);

        int last = ctx.Activations.Length - 1;
        int pred;
        bool isExploration = false;

        float adaptiveEpsilon = epsilon * (1f + Math.Max(0f, 1.5f - ctx.AverageEntropy));

        if (RavineRandom.RangeFloat() < adaptiveEpsilon)
        {
            pred          = RavineRandom.RangeInt(0, ctx.Activations[last].Length);
            isExploration = true;
        }
        else
        {
            pred = ArgMax(ctx.Activations[last]);
        }

        float entropy = CalculateOutputEntropy(ctx.Activations[last]);
        ctx.AverageEntropy = ctx.AverageEntropy * (1f - ctx.Params.EntropyAlpha)
                           + entropy * ctx.Params.EntropyAlpha;

        var item = new DelayedItem(CopyInput(input), pred);
        if (isExploration) item.Evaluation += ctx.Params.ExplorationPrice;
        ctx.DelayedList.Add(item);

        if (ctx.DelayedList.Count > delaySteps)
        {
            var delayed = ctx.DelayedList[0];
            ctx.DelayedList.RemoveAt(0);

            if (Math.Abs(delayed.Evaluation - ctx.Params.DefaultEvaluation) > 0.05f)
            {
                float reward = delayed.Evaluation > ctx.Params.DefaultEvaluation
                    ? delayed.Evaluation
                    : 1f - delayed.Evaluation;
                Train(delayed.Input, delayed.Predicted, reward, ctx);
            }
        }

        return pred;
    }

    // Hogwild: обновляет общие _weights/_biases через контекст конкретной сущности
    public void Train(float[] input, int predictedIndex, float reward, PerceptronContext ctx)
    {
        ctx.TrainingSteps++;

        float lrSchedule  = (float)Math.Exp(-ctx.TrainingSteps * 0.0002f);
        float entropyBoost = Math.Min(2f, 1f + (1.5f - ctx.AverageEntropy) * 0.3f);
        float lr           = ctx.Params.BaseLearningRate
                           * Math.Min(reward, 1.2f)
                           * lrSchedule * entropyBoost;

        float[] noisedInput = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
            noisedInput[i] = input[i] + RavineRandom.RangeFloat(
                -ctx.Params.GaussianNoise, ctx.Params.GaussianNoise);

        ForwardPass(noisedInput, ctx);

        int     last     = ctx.Activations.Length - 1;
        int     outCount = ctx.Activations[last].Length;
        float[] outErr   = new float[outCount];

        float pos = 1f - ctx.Params.LabelSmoothing;
        float neg = ctx.Params.LabelSmoothing / (outCount - 1);

        for (int i = 0; i < outCount; i++)
        {
            float target = (i == predictedIndex) ? pos : neg;
            float act    = ctx.Activations[last][i];
            outErr[i] = (target - act) / (act * (1f - act) + 1e-8f)
                      + ctx.Params.EntropyRegularization * (0.5f - act);
        }

        float[] currentErrors = outErr;
        for (int layer = _weights.Length - 1; layer >= 0; layer--)
        {
            float[] prevActs = ctx.Activations[layer];
            BackpropagateLayer(layer, currentErrors, prevActs, lr, ctx);

            if (layer > 0)
                currentErrors = ComputeErrors(_weights[layer], currentErrors, ctx.Activations[layer]);
        }
    }

    private void ForwardPass(float[] input, PerceptronContext ctx)
    {
        Array.Copy(input, ctx.Activations[0], input.Length);
        for (int l = 0; l < _weights.Length; l++)
            ComputeLayer(l, ctx);
    }

    private void ComputeLayer(int layer, PerceptronContext ctx)
    {
        float[] inp    = ctx.Activations[layer];
        float[] output = ctx.Activations[layer + 1];
        bool    isLast = layer == _weights.Length - 1;

        for (int n = 0; n < output.Length; n++)
        {
            float sum = _biases[layer][n];
            for (int i = 0; i < inp.Length; i++)
                sum += _weights[layer][n][i] * inp[i];
            output[n] = isLast ? sum : LeakyReLU(sum);
        }

        if (isLast)
        {
            var soft = SoftmaxWithTemperature(output, ctx.Params.SoftmaxTemperature);
            Array.Copy(soft, output, output.Length);
        }
    }

    private void BackpropagateLayer(
        int layerIdx, float[] errors, float[] prevActs, float lr, PerceptronContext ctx)
    {
        float adaptiveLambda = ctx.Params.Lambda * (2f - ctx.AverageEntropy);
        float maxGrad        = ctx.Params.MaxGradientNorm;

        for (int i = 0; i < errors.Length; i++)
        {
            for (int j = 0; j < prevActs.Length; j++)
            {
                float g = Mathf.Clamp(lr * errors[i] * prevActs[j], -maxGrad, maxGrad);
                _weights[layerIdx][i][j] += g - adaptiveLambda * _weights[layerIdx][i][j];
            }
            _biases[layerIdx][i] += Mathf.Clamp(lr * errors[i], -maxGrad, maxGrad);
        }
    }

    private static float[] ComputeErrors(
        float[][] nextWeights, float[] nextErrors, float[] activations)
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

    private void InitWeightsAndBiases(int[] layerSizes)
    {
        int L    = layerSizes.Length - 1;
        _weights = new float[L][][];
        _biases  = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _weights[l] = InitWeights(layerSizes[l + 1], layerSizes[l]);
            _biases[l]  = InitBiases(layerSizes[l + 1]);
        }
    }

    private void CloneWeights(DelayedPerceptron src)
    {
        int L    = src._weights.Length;
        _weights = new float[L][][];
        _biases  = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _biases[l] = (float[])src._biases[l].Clone();
            _weights[l] = new float[src._weights[l].Length][];
            for (int n = 0; n < src._weights[l].Length; n++)
                _weights[l][n] = (float[])src._weights[l][n].Clone();
        }
    }

    private void MutateWeights(GeneticParameters p)
    {
        for (int L = 0; L < _weights.Length; L++)
        {
            float strength = L + 1 < layerStrength.Length ? layerStrength[L + 1] : 1f;
            bool isEarlyLayer = L <= 1;

            for (int n = 0; n < _weights[L].Length; n++)
            {
                for (int i = 0; i < _weights[L][n].Length; i++)
                {
                    if (!isEarlyLayer && RavineRandom.RangeFloat() < p.MutationChance)
                        _weights[L][n][i] += RavineRandom.RangeFloat(-p.MutationStrength, p.MutationStrength)
                                           * p.BaseDelta * strength;
                }

                if (!isEarlyLayer && RavineRandom.RangeFloat() < p.MutationChance)
                    _biases[L][n] += RavineRandom.RangeFloat(-p.MutationStrength, p.MutationStrength)
                                   * p.BaseDelta * strength;
            }
        }
    }

    private static float[][] InitWeights(int neurons, int inputs)
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
                float g  = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
                weights[i][j] = g * scale;
            }
        }
        return weights;
    }

    private static float[] InitBiases(int neurons)
    {
        var b = new float[neurons];
        for (int i = 0; i < neurons; i++)
            b[i] = RavineRandom.RangeFloat(-GeneticParameters.Default.InitBiasesValues,
                                            GeneticParameters.Default.InitBiasesValues);
        return b;
    }

    private int ArgMax(float[] arr)
    {
        var pairs = arr
            .AsValueEnumerable()
            .Select((v, i) => (value: v, idx: i))
            .OrderByDescending(p => p.value)
            .Take(3)
            .ToArray();

        float pick = RavineRandom.RangeFloat(), cum = 0f;
        for (int k = 0; k < 3; k++)
        {
            cum += w[k];
            if (pick <= cum) return pairs[k].idx;
        }
        return pairs[0].idx;
    }

    private static float CalculateOutputEntropy(float[] outputs)
    {
        float e = 0f;
        for (int i = 0; i < outputs.Length; i++)
            if (outputs[i] > 1e-8f) e -= outputs[i] * (float)Math.Log(outputs[i]);
        return e;
    }

    private static float[] SoftmaxWithTemperature(float[] vals, float temp)
    {
        float max = vals.AsValueEnumerable().Max();
        var   exp = new float[vals.Length];
        float sum = 0f;
        for (int i = 0; i < vals.Length; i++) { exp[i] = (float)Math.Exp((vals[i] - max) / temp); sum += exp[i]; }
        for (int i = 0; i < exp.Length; i++)  exp[i] /= sum;
        return exp;
    }

    private static float[] CopyInput(float[] src)
    {
        var copy = new float[src.Length];
        Array.Copy(src, copy, src.Length);
        return copy;
    }

    public static float LeakyReLU(float x, float alpha = 0.01f)      => x >= 0 ? x : alpha * x;
    public static float LeakyReLUPrime(float x, float alpha = 0.01f) => x >= 0 ? 1f : alpha;

    public GeneticParameters GetGeneticParameters(PerceptronContext ctx) => ctx.Params;
}