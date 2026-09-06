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
    public const float TauEpsilon = 1e-4f;

    public int[] LayerSizes { get; private set; }

    private SharedGradientAccumulator _gradScratch;
    private SharedGradientAccumulator _gradAccum;

    public float OptimizerMaxGradNorm = 1f;

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize)
        : this(new[] { inputSize, h1, h2, h3, outputSize }) { }

    public DelayedPerceptron(int[] layerSizes)
    {
        LayerSizes = layerSizes;
        InitWeightsAndBiases(LayerSizes);
        BuildResidualMask();
        InitOptimizerBuffers();
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        LayerSizes = parent.LayerSizes;
        CloneWeights(parent);
        BuildResidualMask();
        InitOptimizerBuffers();
    }

    private void InitOptimizerBuffers()
    {
        _gradScratch = new SharedGradientAccumulator(LayerSizes);
        _gradAccum   = new SharedGradientAccumulator(LayerSizes);
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

        int slot = ForwardPass(input, ctx, out int stamp);

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
        item.BpttSlot       = slot;
        item.BpttStamp      = stamp;
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

    private int ForwardPass(float[] input, PerceptronContext ctx, out int stamp)
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

                float tau = MathF.Max(Softplus(preTau), TauEpsilon);
                float A   = 1f + dt / tau;

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

        stamp               = ctx.NextForwardStamp();
        ctx.SlotStamp[slot] = stamp;

        ctx.BpttPtr = (slot + 1) % ctx.TruncWindow;
        if (ctx.BpttCount < ctx.TruncWindow) ctx.BpttCount++;

        return slot;
    }

    public void Train(DelayedItem ticket, float advantage, PerceptronContext ctx)
    {
        if (!float.IsFinite(advantage))
        {
            ctx.Diagnostics.RecordNonFiniteGradient(1);
            return;
        }

        ctx.TrainingSteps++;

        int   L     = _weights.Length;
        int   steps = ResolveBpttSteps(ticket, ctx);
        float dt    = ctx.DeltaTime;

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

        var g = _gradScratch;
        g.Clear();

        for (int l = 0; l < L; l++)
            Array.Clear(ctx.TemporalDeltaH[l], 0, ctx.TemporalDeltaH[l].Length);

        int nonFinite = 0;

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
                    if (!float.IsFinite(dH)) { wDH[n] = 0f; nonFinite++; continue; }

                    float fn   = fArr[n];
                    float taun = MathF.Max(tauArr[n], TauEpsilon);
                    float An   = aArr[n];

                    float dPreF = dH * (dt / An) * (1f - fn * fn);
                    float hNew  = (hBef[n] + dt * fn) / An;
                    float dTau  = dH * hNew * dt / (An * taun * taun);
                    float dPreT = dTau * (1f - MathF.Exp(-taun));

                    if (!float.IsFinite(dPreF) || !float.IsFinite(dPreT))
                    {
                        tempDH[n] = 0f;
                        nonFinite++;
                        continue;
                    }

                    tempDH[n] = dH / An;

                    float[] wRow = _weights[l][n];
                    float[] tRow = _tauWeights[l][n];

                    int wi = g.WeightIndex(l, n);
                    g.MarkTouched(l, n);

                    for (int i = 0; i < prevActs.Length; i++)
                    {
                        float a = prevActs[i];

                        if (prevWDH != null)
                        {
                            float back = dPreF * wRow[i] + dPreT * tRow[i];
                            if (float.IsFinite(back)) prevWDH[i] += back;
                            else nonFinite++;
                        }

                        g.W[wi + i]   += dPreF * a;
                        g.Tau[wi + i] += dPreT * a;
                    }

                    int bi = g.BiasIndex(l, n);
                    g.B[bi]    += dPreF;
                    g.TauB[bi] += dPreT;
                }
            }
        }

        float norm = (float)Math.Sqrt(g.SquaredNorm());

        if (!float.IsFinite(norm))
        {
            ctx.Diagnostics.RecordNonFiniteGradient(nonFinite + 1);
            return;
        }

        ctx.Diagnostics.RecordGradientNorm(norm);

        float scale = MathF.Min(1f, OptimizerMaxGradNorm / (norm + 1e-8f));
        _gradAccum.AddScaled(g, scale);

        if (nonFinite > 0) ctx.Diagnostics.RecordNonFiniteGradient(nonFinite);
    }

    public void ApplyAccumulatedGradients(float lr, float weightDecay, BrainDiagnostics diag)
    {
        var acc = _gradAccum;
        if (acc.Contributions == 0) return;

        float inv = lr / acc.Contributions;
        int   nonFinite = 0;

        for (int l = 0; l < _weights.Length; l++)
        {
            int neurons = acc.Neurons(l);
            int inputs  = acc.Inputs(l);

            for (int n = 0; n < neurons; n++)
            {
                if (!acc.IsTouched(l, n)) continue;

                float[] wRow = _weights[l][n];
                float[] tRow = _tauWeights[l][n];
                int wi = acc.WeightIndex(l, n);

                for (int i = 0; i < inputs; i++)
                {
                    float oldW = wRow[i];
                    float oldT = tRow[i];

                    float newW = oldW + acc.W[wi + i]   * inv - weightDecay * oldW;
                    float newT = oldT + acc.Tau[wi + i] * inv - weightDecay * oldT;

                    if (float.IsFinite(newW)) wRow[i] = newW; else nonFinite++;
                    if (float.IsFinite(newT)) tRow[i] = newT; else nonFinite++;
                }

                int bi = acc.BiasIndex(l, n);
                float newBF = _biases[l][n]    + acc.B[bi]    * inv;
                float newBT = _tauBiases[l][n] + acc.TauB[bi] * inv;

                if (float.IsFinite(newBF)) _biases[l][n]    = newBF; else nonFinite++;
                if (float.IsFinite(newBT)) _tauBiases[l][n] = newBT; else nonFinite++;
            }
        }

        if (nonFinite > 0) diag?.RecordNonFiniteGradient(nonFinite);
        acc.Clear();
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

    public void FlushTerminal(PerceptronContext ctx, ValueCritic critic, float gamma, float penalty)
    {
        var ring  = ctx.Decisions;
        int count = ring.Count;

        for (int i = 0; i < count; i++)
        {
            var item = ring[i];
            if (item.Trained) continue;

            bool  last     = i == count - 1;
            float reward   = item.Evaluation + (last ? penalty : 0f);
            float tdTarget = last ? reward : reward + gamma * critic.Predict(ring[i + 1].State);

            float advantage = critic.TrainTD(item.State, tdTarget);

            ctx.Diagnostics.RecordAdvantage(advantage);
            ctx.Diagnostics.RecordCriticError(advantage);

            item.Trained = true;
            if (MathF.Abs(advantage) > 0.05f)
                Train(item, advantage, ctx);
        }

        ring.Clear();
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

    private static int ResolveBpttSteps(DelayedItem ticket, PerceptronContext ctx)
    {
        int w = ctx.TruncWindow;

        if (ctx.SlotStamp[ticket.BpttSlot] != ticket.BpttStamp)
        {
            ctx.Diagnostics.RecordStaleSlotDrop();
            return 1;
        }

        int max = Math.Min(ctx.BpttCount, w);
        for (int step = 1; step < max; step++)
        {
            int t = (ticket.BpttSlot - step + w * 2) % w;
            if (ctx.SlotStamp[t] != ticket.BpttStamp - step) return step;
        }
        return max;
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