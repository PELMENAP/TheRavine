using System;
using UnityEngine;
using TheRavine.Extensions;

public partial class DelayedPerceptron
{
    private float[][][] _weights;
    private float[][][] _tauWeights;
    private float[][]   _biases;
    private float[][]   _tauBiases;

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
    }

    public PerceptronContext CreateContext(GeneticParameters? p = null, int truncWindow = 8)
        => new PerceptronContext(LayerSizes, p ?? GeneticParameters.Default, truncWindow);

    public int Predict(float[] input, PerceptronContext ctx, int delaySteps, float epsilon = 0.1f)
    {
        ForwardPass(input, ctx);

        int   last            = ctx.Activations.Length - 1;
        float adaptiveEpsilon = epsilon * (1f + MathF.Max(0f, 1.5f - ctx.AverageEntropy));
        bool  isExploration   = RavineRandom.RangeFloat() < adaptiveEpsilon;

        int pred = isExploration
            ? RavineRandom.RangeInt(0, ctx.Activations[last].Length)
            : RouletteWheelSelection(ctx.Activations[last]);

        float entropy      = CalculateOutputEntropy(ctx.Activations[last]);
        ctx.AverageEntropy = ctx.AverageEntropy * (1f - ctx.Params.EntropyAlpha)
                           + entropy * ctx.Params.EntropyAlpha;

        var item = new DelayedItem(pred);
        if (isExploration) item.Evaluation += ctx.Params.ExplorationPrice;
        ctx.DelayedList.Add(item);

        if (ctx.DelayedList.Count > delaySteps)
        {
            var delayed = ctx.DelayedList[0];
            ctx.DelayedList.RemoveAt(0);

            if (MathF.Abs(delayed.Evaluation - ctx.Params.DefaultEvaluation) > 0.05f)
            {
                float r = delayed.Evaluation > ctx.Params.DefaultEvaluation
                    ? delayed.Evaluation
                    : 1f - delayed.Evaluation;
                Train(delayed.Predicted, r, ctx);
            }
        }

        return pred;
    }

    // ForwardPass сохраняет историю в кольцевой буфер — без доп. аллокаций
    private void ForwardPass(float[] input, PerceptronContext ctx)
    {
        float dt   = ctx.DeltaTime;
        int   slot = ctx.BpttPtr;

        Array.Copy(input, ctx.Activations[0], input.Length);

        for (int l = 0; l < _weights.Length; l++)
        {
            float[] inp = ctx.Activations[l];
            float[] h   = ctx.HiddenStates[l];
            float[] act = ctx.Activations[l + 1];

            // Сохраняем состояние ДО обновления (критично для корректности TBPTT)
            Array.Copy(inp, ctx.BpttPrevActs[slot][l], inp.Length);
            Array.Copy(h,   ctx.BpttHBefore[slot][l],  h.Length);

            float[] fSlot   = ctx.BpttF[slot][l];
            float[] tauSlot = ctx.BpttTau[slot][l];
            float[] aSlot   = ctx.BpttA[slot][l];

            for (int n = 0; n < h.Length; n++)
            {
                float[] wRow = _weights[l][n];
                float[] tRow = _tauWeights[l][n];

                float preF = _biases[l][n];
                for (int i = 0; i < inp.Length; i++) preF  += wRow[i] * inp[i];
                float f = MathF.Tanh(preF);

                float preTau = _tauBiases[l][n];
                for (int i = 0; i < inp.Length; i++) preTau += tRow[i] * inp[i];
                float tau = Softplus(preTau);
                float A   = 1f + dt / MathF.Max(tau, 1e-4f);

                ctx.FVals[l][n]   = fSlot[n]   = f;
                ctx.TauVals[l][n] = tauSlot[n] = tau;
                ctx.AVals[l][n]   = aSlot[n]   = A;

                h[n]   = (h[n] + dt * f) / A;
                act[n] = h[n];
            }
        }

        // Softmax в кэш-буфер, без new[]
        int outIdx = _weights.Length;
        SoftmaxInPlace(ctx.Activations[outIdx], ctx.SoftmaxBuf, ctx.Params.SoftmaxTemperature);

        ctx.BpttPtr = (slot + 1) % ctx.TruncWindow;
        if (ctx.BpttCount < ctx.TruncWindow) ctx.BpttCount++;
    }

    // Настоящий Truncated BPTT: разворачиваем W шагов назад,
    // на каждом шаге делаем spatial backprop по слоям
    // и передаём временной градиент δh/A на предыдущий шаг.
    public void Train(int predictedIndex, float reward, PerceptronContext ctx)
    {
        ctx.TrainingSteps++;

        int   L     = _weights.Length;
        int   steps = Math.Min(ctx.BpttCount, ctx.TruncWindow);
        float dt    = ctx.DeltaTime;

        float lrSchedule   = MathF.Exp(-ctx.TrainingSteps * 0.0002f);
        float entropyBoost = MathF.Min(2f, 1f + (1.5f - ctx.AverageEntropy) * 0.3f);
        float lr           = ctx.Params.BaseLearningRate
                           * MathF.Min(reward, 1.2f) * lrSchedule * entropyBoost;
        float lambda  = ctx.Params.Lambda * (2f - ctx.AverageEntropy);
        float maxGrad = ctx.Params.MaxGradientNorm;

        // Вычисляем ошибку выхода по текущим активациям (шаг T)
        int   outCount = ctx.OutErrBuf.Length;
        float pos      = 1f - ctx.Params.LabelSmoothing;
        float neg      = ctx.Params.LabelSmoothing / (outCount - 1);
        int   lastAct  = ctx.Activations.Length - 1;

        for (int i = 0; i < outCount; i++)
        {
            float target   = (i == predictedIndex) ? pos : neg;
            float act      = ctx.Activations[lastAct][i];
            ctx.OutErrBuf[i] = (target - act) + ctx.Params.EntropyRegularization * (0.5f - act);
        }

        // Сброс временного градиента
        for (int l = 0; l < L; l++)
            Array.Clear(ctx.TemporalDeltaH[l], 0, ctx.TemporalDeltaH[l].Length);

        for (int step = 0; step < steps; step++)
        {
            // Кольцевой индекс: step=0 → самый свежий слот
            int t = (ctx.BpttPtr - 1 - step + ctx.TruncWindow * 2) % ctx.TruncWindow;

            // WorkingDeltaH = temporal с шага t+1
            for (int l = 0; l < L; l++)
                Array.Copy(ctx.TemporalDeltaH[l], ctx.WorkingDeltaH[l],
                           ctx.TemporalDeltaH[l].Length);

            // На первом шаге (t=T) добавляем ошибку выхода в последний слой
            if (step == 0)
            {
                float[] wDHLast = ctx.WorkingDeltaH[L - 1];
                for (int i = 0; i < outCount; i++)
                    wDHLast[i] += ctx.OutErrBuf[i];
            }

            // Spatial backprop: от последнего слоя к первому
            for (int l = L - 1; l >= 0; l--)
            {
                float[] prevActs = ctx.BpttPrevActs[t][l];
                float[] hBef     = ctx.BpttHBefore[t][l];
                float[] fArr     = ctx.BpttF[t][l];
                float[] tauArr   = ctx.BpttTau[t][l];
                float[] aArr     = ctx.BpttA[t][l];
                float[] wDH      = ctx.WorkingDeltaH[l];
                float[] tempDH   = ctx.TemporalDeltaH[l];
                float[] prevWDH  = l > 0 ? ctx.WorkingDeltaH[l - 1] : null;

                Array.Clear(tempDH, 0, tempDH.Length);

                for (int n = 0; n < wDH.Length; n++)
                {
                    float dH = wDH[n];
                    if (dH == 0f) continue;

                    float fn   = fArr[n];
                    float taun = tauArr[n];
                    float An   = aArr[n];

                    // δpreF = δh · (dt/A) · (1 − f²)
                    float dPreF = dH * (dt / An) * (1f - fn * fn);

                    // hNew = (hBef + dt·f) / A  — восстанавливаем из сохранённой истории
                    float hNew  = (hBef[n] + dt * fn) / An;

                    // δτ = δh · hNew · dt / (A · τ²)
                    // δpreTau = δτ · (1 − exp(−τ))  ← softplus'(preTau) = sigmoid(preTau)
                    float dTau  = dH * hNew * dt / (An * taun * taun);
                    float dPreT = dTau * (1f - MathF.Exp(-taun));

                    // Временной градиент на шаг t−1: δh[t−1] = δh[t] / A
                    tempDH[n] = dH / An;

                    float[] wRow = _weights[l][n];
                    float[] tRow = _tauWeights[l][n];

                    // Один проход по prevActs: spatial gradient + weight update
                    // Читаем старые веса ДО модификации → корректная spatial grad
                    for (int i = 0; i < prevActs.Length; i++)
                    {
                        float oldW = wRow[i];
                        float oldT = tRow[i];

                        if (prevWDH != null)
                            prevWDH[i] += dPreF * oldW + dPreT * oldT;

                        float gF   = Mathf.Clamp(lr * dPreF * prevActs[i], -maxGrad, maxGrad);
                        float gTau = Mathf.Clamp(lr * dPreT * prevActs[i], -maxGrad, maxGrad);

                        // Запись через локальные переменные: L2 применяется к old-весу
                        wRow[i] = oldW + gF   - lambda * oldW;
                        tRow[i] = oldT + gTau - lambda * oldT;
                    }

                    _biases[l][n]    += Mathf.Clamp(lr * dPreF, -maxGrad, maxGrad);
                    _tauBiases[l][n] += Mathf.Clamp(lr * dPreT, -maxGrad, maxGrad);
                }
            }
        }
    }

    // Пишем прямо в vals через промежуточный buf — ноль аллокаций
    private static void SoftmaxInPlace(float[] vals, float[] buf, float temp)
    {
        float max = vals[0];
        for (int i = 1; i < vals.Length; i++)
            if (vals[i] > max) max = vals[i];

        float sum = 0f;
        for (int i = 0; i < vals.Length; i++)
        { buf[i] = MathF.Exp((vals[i] - max) / temp); sum += buf[i]; }

        float inv = 1f / sum;
        for (int i = 0; i < vals.Length; i++) vals[i] = buf[i] * inv;
    }

    private int RouletteWheelSelection(float[] probs)
    {
        float pick = RavineRandom.RangeFloat(), cum = 0f;
        for (int i = 0; i < probs.Length; i++)
        {
            cum += probs[i];
            if (pick <= cum) return i;
        }
        return probs.Length - 1;
    }

    private static float CalculateOutputEntropy(float[] outputs)
    {
        float e = 0f;
        for (int i = 0; i < outputs.Length; i++)
            if (outputs[i] > 1e-8f) e -= outputs[i] * MathF.Log(outputs[i]);
        return e;
    }

    public static float Softplus(float x)
        => x > 20f ? x : MathF.Log(1f + MathF.Exp(x));

    private void InitWeightsAndBiases(int[] layerSizes)
    {
        int L       = layerSizes.Length - 1;
        _weights    = new float[L][][];
        _tauWeights = new float[L][][];
        _biases     = new float[L][];
        _tauBiases  = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _weights[l]    = InitWeights(layerSizes[l + 1], layerSizes[l]);
            _tauWeights[l] = InitTauWeights(layerSizes[l + 1], layerSizes[l]);
            _biases[l]     = InitBiases(layerSizes[l + 1]);
            _tauBiases[l]  = new float[layerSizes[l + 1]];
        }
    }

    private static float[][] InitWeights(int neurons, int inputs)
    {
        float scale   = MathF.Sqrt(2f / (neurons + inputs));
        var   weights = new float[neurons][];
        for (int i = 0; i < neurons; i++)
        {
            weights[i] = new float[inputs];
            for (int j = 0; j < inputs; j++)
            {
                float u1 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                float u2 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                weights[i][j] = MathF.Sqrt(-2f * MathF.Log(u1))
                              * MathF.Cos(2f * MathF.PI * u2) * scale;
            }
        }
        return weights;
    }

    private static float[][] InitTauWeights(int neurons, int inputs)
    {
        float scale   = 0.1f / MathF.Sqrt(inputs);
        var   weights = new float[neurons][];
        for (int i = 0; i < neurons; i++)
        {
            weights[i] = new float[inputs];
            for (int j = 0; j < inputs; j++)
                weights[i][j] = RavineRandom.RangeFloat(-scale, scale);
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

    private void CloneWeights(DelayedPerceptron src)
    {
        int L       = src._weights.Length;
        _weights    = new float[L][][];
        _tauWeights = new float[L][][];
        _biases     = new float[L][];
        _tauBiases  = new float[L][];

        for (int l = 0; l < L; l++)
        {
            _biases[l]    = (float[])src._biases[l].Clone();
            _tauBiases[l] = (float[])src._tauBiases[l].Clone();

            _weights[l]    = new float[src._weights[l].Length][];
            _tauWeights[l] = new float[src._tauWeights[l].Length][];
            for (int n = 0; n < src._weights[l].Length; n++)
            {
                _weights[l][n]    = (float[])src._weights[l][n].Clone();
                _tauWeights[l][n] = (float[])src._tauWeights[l][n].Clone();
            }
        }
    }

    public GeneticParameters GetGeneticParameters(PerceptronContext ctx) => ctx.Params;
}
public class DelayedItem
{
    public int   Predicted  { get; }
    public float Evaluation { get; set; }

    public DelayedItem(int predicted)
    {
        Predicted  = predicted;
        Evaluation = 0.5f;
    }
}