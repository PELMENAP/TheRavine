using System;
using UnityEngine;

public partial class DelayedPerceptron
{
    private float[][][] _weights;
    private float[][][] _tauWeights;
    private float[][]   _biases;
    private float[][]   _tauBiases;
    private bool[]      _residual;

    public const float DurationNoiseSigma = 0.5f;

    public int[] LayerSizes { get; private set; }

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize)
        : this(new[] { inputSize, h1, h2, h3, outputSize }) { }

    public DelayedPerceptron(int[] layerSizes)
    {
        LayerSizes = layerSizes;
        InitWeightsAndBiases(LayerSizes);
        BuildResidualMask();
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        LayerSizes = parent.LayerSizes;
        CloneWeights(parent);
        BuildResidualMask();
    }

    private void BuildResidualMask()
    {
        int L = LayerSizes.Length - 1;
        _residual = new bool[L];
        for (int l = 1; l < L - 1; l++)
            _residual[l] = LayerSizes[l] == LayerSizes[l + 1];
    }

    public bool HasResidual(int layer) => _residual[layer];

    public PerceptronContext CreateContext(GeneticParameters? p = null,
        int truncWindow = 8, int decisionCapacity = 16)
        => new PerceptronContext(LayerSizes, p ?? GeneticParameters.Default, truncWindow, decisionCapacity);

    public static float DurationFromLogit(float logit, float minDuration, float maxDuration)
    {
        float s = 1f / (1f + MathF.Exp(-logit));
        return minDuration + s * (maxDuration - minDuration);
    }

    public DelayedItem Decide(float[] input, PerceptronContext ctx, int delaySteps,
        ValueCritic critic, float gamma, float dt, float simTime,
        float minDuration, float maxDuration, float epsilon)
    {
        ctx.DeltaTime = dt > 0f ? dt : ctx.DeltaTime;

        int slot = ForwardPass(input, ctx);

        int   last            = ctx.Activations.Length - 1;
        float[] outAct        = ctx.Activations[last];
        int   actionCount     = ctx.ActionCount;
        float adaptiveEpsilon = epsilon * (1f + MathF.Max(0f, 1.5f - ctx.AverageEntropy));
        bool  isExploration   = RavineRandom.RangeFloat() < adaptiveEpsilon;

        int pred = isExploration
            ? RavineRandom.RangeInt(0, actionCount)
            : RouletteWheelSelection(outAct, actionCount);

        float entropy      = CalculateOutputEntropy(outAct, actionCount);
        ctx.AverageEntropy = ctx.AverageEntropy * (1f - ctx.Params.EntropyAlpha)
                           + entropy * ctx.Params.EntropyAlpha;
        ctx.Diagnostics.RecordEntropy(entropy);

        if (ctx.Decisions.Count >= ctx.Decisions.Capacity)
            FlushOldest(ctx, input, critic, gamma);

        var item = ctx.Decisions.Push();
        item.DecisionId     = ctx.NextDecisionId();
        item.Predicted      = pred;
        item.BpttSlot       = slot;
        item.StartTime      = simTime;
        item.ValueEstimate  = critic.Predict(input);
        item.LogProbability = MathF.Log(MathF.Max(outAct[pred], 1e-8f));
        Array.Copy(input, item.State, input.Length);
        Array.Copy(outAct, item.Probs, actionCount);

        float baseLogit = ctx.Activations[last][ctx.DurationIndex];
        float noise     = SampleGaussian() * DurationNoiseSigma;
        item.DurationLogit = baseLogit;
        item.DurationNoise = noise;
        item.Duration      = DurationFromLogit(baseLogit + noise, minDuration, maxDuration);

        if (isExploration) item.Evaluation += ctx.Params.ExplorationPrice;

        ctx.Diagnostics.RecordDecision(item.Duration);

        while (ctx.Decisions.Count > delaySteps)
            FlushOldest(ctx, input, critic, gamma);

        return item;
    }

    private void FlushOldest(PerceptronContext ctx, float[] nextState, ValueCritic critic, float gamma)
    {
        var delayed = ctx.Decisions.Oldest;
        if (delayed == null) return;
        ctx.Decisions.PopOldest();
        if (delayed.Trained) return;

        float vNext     = critic.Predict(nextState);
        float tdTarget  = delayed.Evaluation + gamma * vNext;
        float advantage = critic.TrainTD(delayed.State, tdTarget);

        ctx.Diagnostics.RecordAdvantage(advantage);
        ctx.Diagnostics.RecordCriticError(advantage);

        delayed.Trained = true;
        if (MathF.Abs(advantage) > 0.05f)
            Train(delayed, advantage, ctx);
    }

    private int ForwardPass(float[] input, PerceptronContext ctx)
    {
        float dt   = ctx.DeltaTime;
        int   slot = ctx.BpttPtr;

        Array.Copy(input, ctx.Activations[0], input.Length);

        for (int l = 0; l < _weights.Length; l++)
        {
            float[] inp = ctx.Activations[l];
            float[] h   = ctx.HiddenStates[l];
            float[] act = ctx.Activations[l + 1];

            Array.Copy(inp, ctx.BpttPrevActs[slot][l], inp.Length);
            Array.Copy(h,   ctx.BpttHBefore[slot][l],  h.Length);

            float[] fSlot   = ctx.BpttF[slot][l];
            float[] tauSlot = ctx.BpttTau[slot][l];
            float[] aSlot   = ctx.BpttA[slot][l];
            bool    res     = _residual[l];

            for (int n = 0; n < h.Length; n++)
            {
                float[] wRow = _weights[l][n];
                float[] tRow = _tauWeights[l][n];

                float preF = _biases[l][n];
                for (int i = 0; i < inp.Length; i++) preF += wRow[i] * inp[i];
                float f = MathF.Tanh(preF);

                float preTau = _tauBiases[l][n];
                for (int i = 0; i < inp.Length; i++) preTau += tRow[i] * inp[i];
                float tau = Softplus(preTau);
                float A   = 1f + dt / MathF.Max(tau, 1e-4f);

                ctx.FVals[l][n]   = fSlot[n]   = f;
                ctx.TauVals[l][n] = tauSlot[n] = tau;
                ctx.AVals[l][n]   = aSlot[n]   = A;

                h[n]   = (h[n] + dt * f) / A;
                act[n] = res ? h[n] + inp[n] : h[n];
            }
        }

        int outIdx = _weights.Length;
        SoftmaxInPlace(ctx.Activations[outIdx], ctx.SoftmaxBuf,
                       ctx.ActionCount, ctx.Params.SoftmaxTemperature);

        ctx.BpttPtr = (slot + 1) % ctx.TruncWindow;
        if (ctx.BpttCount < ctx.TruncWindow) ctx.BpttCount++;

        return outIdx == 0 ? 0 : (ctx.BpttPtr - 1 + ctx.TruncWindow) % ctx.TruncWindow;
    }

    public void Train(DelayedItem ticket, float advantage, PerceptronContext ctx)
    {
        ctx.TrainingSteps++;

        int   L     = _weights.Length;
        int   steps = Math.Min(ctx.BpttCount, ctx.TruncWindow);
        float dt    = ctx.DeltaTime;

        float lrSchedule = MathF.Exp(-ctx.TrainingSteps * 0.0002f);
        float lr         = ctx.Params.BaseLearningRate * lrSchedule;
        float lambda     = ctx.Params.Lambda;
        float maxGrad    = ctx.Params.MaxGradientNorm;

        int   actionCount = ctx.ActionCount;
        float invN        = 1f / actionCount;
        float entReg      = ctx.Params.EntropyRegularization;
        int   pred        = ticket.Predicted;

        for (int i = 0; i < actionCount; i++)
        {
            float oneHot = i == pred ? 1f : 0f;
            float p      = ticket.Probs[i];
            ctx.OutErrBuf[i] = (oneHot - p) * advantage + entReg * (invN - p);
        }

        ctx.OutErrBuf[ctx.DurationIndex] =
            advantage * ticket.DurationNoise / (DurationNoiseSigma * DurationNoiseSigma);

        for (int l = 0; l < L; l++)
            Array.Clear(ctx.TemporalDeltaH[l], 0, ctx.TemporalDeltaH[l].Length);

        float gradSq = 0f;

        for (int step = 0; step < steps; step++)
        {
            int t = (ticket.BpttSlot - step + ctx.TruncWindow * 2) % ctx.TruncWindow;

            for (int l = 0; l < L; l++)
                Array.Copy(ctx.TemporalDeltaH[l], ctx.WorkingDeltaH[l],
                           ctx.TemporalDeltaH[l].Length);

            if (step == 0)
            {
                float[] wDHLast = ctx.WorkingDeltaH[L - 1];
                for (int i = 0; i < ctx.OutputSize; i++)
                    wDHLast[i] += ctx.OutErrBuf[i];
            }

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

                if (_residual[l] && prevWDH != null)
                    for (int i = 0; i < wDH.Length; i++)
                        prevWDH[i] += wDH[i];

                for (int n = 0; n < wDH.Length; n++)
                {
                    float dH = wDH[n];
                    if (dH == 0f) continue;

                    float fn   = fArr[n];
                    float taun = tauArr[n];
                    float An   = aArr[n];

                    float dPreF = dH * (dt / An) * (1f - fn * fn);
                    float hNew  = (hBef[n] + dt * fn) / An;
                    float dTau  = dH * hNew * dt / (An * taun * taun);
                    float dPreT = dTau * (1f - MathF.Exp(-taun));

                    tempDH[n] = dH / An;

                    float[] wRow = _weights[l][n];
                    float[] tRow = _tauWeights[l][n];

                    for (int i = 0; i < prevActs.Length; i++)
                    {
                        float oldW = wRow[i];
                        float oldT = tRow[i];

                        if (prevWDH != null)
                            prevWDH[i] += dPreF * oldW + dPreT * oldT;

                        float gF   = Mathf.Clamp(lr * dPreF * prevActs[i], -maxGrad, maxGrad);
                        float gTau = Mathf.Clamp(lr * dPreT * prevActs[i], -maxGrad, maxGrad);

                        gradSq += gF * gF + gTau * gTau;

                        wRow[i] = oldW + gF   - lambda * oldW;
                        tRow[i] = oldT + gTau - lambda * oldT;
                    }

                    float bF = Mathf.Clamp(lr * dPreF, -maxGrad, maxGrad);
                    float bT = Mathf.Clamp(lr * dPreT, -maxGrad, maxGrad);
                    gradSq += bF * bF + bT * bT;

                    _biases[l][n]    += bF;
                    _tauBiases[l][n] += bT;
                }
            }
        }

        ctx.Diagnostics.RecordGradientNorm(MathF.Sqrt(gradSq));
    }

    private static void SoftmaxInPlace(float[] vals, float[] buf, int count, float temp)
    {
        float max = vals[0];
        for (int i = 1; i < count; i++)
            if (vals[i] > max) max = vals[i];

        float sum = 0f;
        for (int i = 0; i < count; i++)
        { buf[i] = MathF.Exp((vals[i] - max) / temp); sum += buf[i]; }

        float inv = 1f / sum;
        for (int i = 0; i < count; i++) vals[i] = buf[i] * inv;
    }

    private int RouletteWheelSelection(float[] probs, int count)
    {
        float pick = RavineRandom.RangeFloat(), cum = 0f;
        for (int i = 0; i < count; i++)
        {
            cum += probs[i];
            if (pick <= cum) return i;
        }
        return count - 1;
    }

    private static float CalculateOutputEntropy(float[] outputs, int count)
    {
        float e = 0f;
        for (int i = 0; i < count; i++)
            if (outputs[i] > 1e-8f) e -= outputs[i] * MathF.Log(outputs[i]);
        return e;
    }

    private static float SampleGaussian()
    {
        float u1 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
        float u2 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
        return MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
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