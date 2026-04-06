using System;
using UnityEngine;
using ZLinq;
using TheRavine.Extensions;

public partial class DelayedPerceptron
{
    private float[][][] _weights;
    private float[][][] _tauWeights;  // веса для τ(I)
    private float[][]   _biases;
    private float[][]   _tauBiases;  // biases для τ
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

    private PerceptronContext MakeTempContext() => CreateContext();

    public int Predict(float[] input, PerceptronContext ctx, int delaySteps, float epsilon = 0.1f)
    {
        ForwardPass(input, ctx);

        int   last            = ctx.Activations.Length - 1;
        float adaptiveEpsilon = epsilon * (1f + Math.Max(0f, 1.5f - ctx.AverageEntropy));

        bool isExploration = RavineRandom.RangeFloat() < adaptiveEpsilon;

        int pred = isExploration
            ? RavineRandom.RangeInt(0, ctx.Activations[last].Length)
            : RouletteWheelSelection(ctx.Activations[last]);

        float entropy      = CalculateOutputEntropy(ctx.Activations[last]);
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

    private void ForwardPass(float[] input, PerceptronContext ctx)
    {
        float dt = ctx.DeltaTime;
        Array.Copy(input, ctx.Activations[0], input.Length);

        for (int l = 0; l < _weights.Length; l++)
        {
            float[] inp  = ctx.Activations[l];
            float[] h    = ctx.HiddenStates[l];
            float[] act  = ctx.Activations[l + 1];
            bool    last = l == _weights.Length - 1;

            for (int n = 0; n < h.Length; n++)
            {
                float preF = _biases[l][n];
                for (int i = 0; i < inp.Length; i++)
                    preF += _weights[l][n][i] * inp[i];
                float f = (float)Math.Tanh(preF);
                ctx.FVals[l][n] = f;

                float preTau = _tauBiases[l][n];
                for (int i = 0; i < inp.Length; i++)
                    preTau += _tauWeights[l][n][i] * inp[i];
                float tau = Softplus(preTau);
                ctx.TauVals[l][n] = tau;

                float A = 1f + dt / MathF.Max(tau, 1e-4f);
                ctx.AVals[l][n] = A;

                h[n]   = (h[n] + dt * f) / A;
                act[n] = h[n];
            }
        }

        // Softmax только на выходном слое
        int outIdx = _weights.Length;
        var soft = SoftmaxWithTemperature(ctx.Activations[outIdx], ctx.Params.SoftmaxTemperature);
        Array.Copy(soft, ctx.Activations[outIdx], soft.Length);
    }

    private void BackpropagateLayer(
        int layerIdx, float[] errors, float[] prevActs, float lr, PerceptronContext ctx)
    {
        float lambda  = ctx.Params.Lambda * (2f - ctx.AverageEntropy);
        float maxGrad = ctx.Params.MaxGradientNorm;
        float dt      = ctx.DeltaTime;

        for (int n = 0; n < errors.Length; n++)
        {
            float err  = errors[n];
            float A    = ctx.AVals[layerIdx][n];
            float f    = ctx.FVals[layerIdx][n];
            float tau  = ctx.TauVals[layerIdx][n];
            float hNew = ctx.HiddenStates[layerIdx][n];

            // ∂L/∂preF = err * (dt/A) * (1 - f²)
            float dLdPreF = err * (dt / A) * (1f - f * f);

            // ∂L/∂tau = err * hNew * dt / (A * tau²)
            // ∂L/∂preTau = ∂L/∂tau * softplus'(preTau) = ∂L/∂tau * (1 - exp(-tau))
            float dLdTau  = err * hNew * dt / (A * tau * tau);
            float dLdPreT = dLdTau * (1f - MathF.Exp(-tau));

            for (int i = 0; i < prevActs.Length; i++)
            {
                float gF   = Mathf.Clamp(lr * dLdPreF * prevActs[i], -maxGrad, maxGrad);
                float gTau = Mathf.Clamp(lr * dLdPreT * prevActs[i], -maxGrad, maxGrad);

                _weights[layerIdx][n][i]    += gF   - lambda * _weights[layerIdx][n][i];
                _tauWeights[layerIdx][n][i] += gTau - lambda * _tauWeights[layerIdx][n][i];
            }

            _biases[layerIdx][n]    += Mathf.Clamp(lr * dLdPreF, -maxGrad, maxGrad);
            _tauBiases[layerIdx][n] += Mathf.Clamp(lr * dLdPreT, -maxGrad, maxGrad);
        }
    }

    private float[] ComputeErrors(int layerIdx, float[] nextErrors, PerceptronContext ctx)
    {
        float dt    = ctx.DeltaTime;
        var errors  = new float[ctx.Activations[layerIdx].Length];

        for (int n = 0; n < nextErrors.Length; n++)
        {
            float err  = nextErrors[n];
            float A    = ctx.AVals[layerIdx][n];
            float f    = ctx.FVals[layerIdx][n];
            float tau  = ctx.TauVals[layerIdx][n];
            float hNew = ctx.HiddenStates[layerIdx][n];

            float dLdPreF = err * (dt / A) * (1f - f * f);
            float dLdTau  = err * hNew * dt / (A * tau * tau);
            float dLdPreT = dLdTau * (1f - MathF.Exp(-tau));

            for (int i = 0; i < errors.Length; i++)
                errors[i] += dLdPreF * _weights[layerIdx][n][i]
                           + dLdPreT * _tauWeights[layerIdx][n][i];
        }
        return errors;
    }

    public void Train(float[] input, int predictedIndex, float reward, PerceptronContext ctx)
    {
        ctx.TrainingSteps++;

        float lrSchedule   = MathF.Exp(-ctx.TrainingSteps * 0.0002f);
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
            outErr[i]    = (target - act) + ctx.Params.EntropyRegularization * (0.5f - act);
        }

        float[] currentErrors = outErr;
        for (int layer = _weights.Length - 1; layer >= 0; layer--)
        {
            float[] prevActs = ctx.Activations[layer];
            BackpropagateLayer(layer, currentErrors, prevActs, lr, ctx);
            if (layer > 0)
                currentErrors = ComputeErrors(layer, currentErrors, ctx);
        }
    }

    private void InitWeightsAndBiases(int[] layerSizes)
    {
        int L        = layerSizes.Length - 1;
        _weights     = new float[L][][];
        _tauWeights  = new float[L][][];
        _biases      = new float[L][];
        _tauBiases   = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _weights[l]    = InitWeights(layerSizes[l + 1], layerSizes[l]);
            _tauWeights[l] = InitTauWeights(layerSizes[l + 1], layerSizes[l]);
            _biases[l]     = InitBiases(layerSizes[l + 1]);
            _tauBiases[l]  = new float[layerSizes[l + 1]]; // tau bias = 0 → τ ≈ ln2 ≈ 0.69
        }
    }

    private static float[][] InitTauWeights(int neurons, int inputs)
    {
        var weights = new float[neurons][];
        float scale = 0.1f / MathF.Sqrt(inputs);
        for (int i = 0; i < neurons; i++)
        {
            weights[i] = new float[inputs];
            for (int j = 0; j < inputs; j++) // wait, inputs is int, not array
                weights[i][j] = RavineRandom.RangeFloat(-scale, scale);
        }
        return weights;
    }

    private void CloneWeights(DelayedPerceptron src)
    {
        int L        = src._weights.Length;
        _weights     = new float[L][][];
        _tauWeights  = new float[L][][];
        _biases      = new float[L][];
        _tauBiases   = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _biases[l]   = (float[])src._biases[l].Clone();
            _tauBiases[l] = (float[])src._tauBiases[l].Clone();

            _weights[l]   = new float[src._weights[l].Length][];
            _tauWeights[l] = new float[src._tauWeights[l].Length][];

            for (int n = 0; n < src._weights[l].Length; n++)
            {
                _weights[l][n]    = (float[])src._weights[l][n].Clone();
                _tauWeights[l][n] = (float[])src._tauWeights[l][n].Clone();
            }
        }
    }

    private void MutateWeights(GeneticParameters p)
    {
        for (int L = 0; L < _weights.Length; L++)
        {
            float strength    = L + 1 < layerStrength.Length ? layerStrength[L + 1] : 1f;
            bool isEarlyLayer = L <= 1;

            for (int n = 0; n < _weights[L].Length; n++)
            {
                for (int i = 0; i < _weights[L][n].Length; i++)
                {
                    if (!isEarlyLayer && RavineRandom.RangeFloat() < p.MutationChance)
                    {
                        float delta = RavineRandom.RangeFloat(-p.MutationStrength, p.MutationStrength)
                                    * p.BaseDelta * strength;
                        _weights[L][n][i]    += delta;
                        _tauWeights[L][n][i] += delta * 0.3f; // tau-мутация мягче
                    }
                }

                if (!isEarlyLayer && RavineRandom.RangeFloat() < p.MutationChance)
                {
                    float delta = RavineRandom.RangeFloat(-p.MutationStrength, p.MutationStrength)
                                * p.BaseDelta * strength;
                    _biases[L][n]    += delta;
                    _tauBiases[L][n] += delta * 0.3f;
                }
            }
        }
    }
    public static float Softplus(float x)
        => x > 20f ? x : (float)Math.Log(1.0 + Math.Exp(x));

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

    private int RouletteWheelSelection(float[] probabilities)
    {
        float pick = RavineRandom.RangeFloat();
        float cumulative = 0f;

        for (int i = 0; i < probabilities.Length; i++)
        {
            cumulative += probabilities[i];
            if (pick <= cumulative)
                return i;
        }
        
        return probabilities.Length - 1; 
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
    public GeneticParameters GetGeneticParameters(PerceptronContext ctx) => ctx.Params;
}