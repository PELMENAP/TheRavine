using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

public partial class DelayedPerceptron
{
    private float[] _wt;
    private float[] _bt;
    private bool[]  _residual;

    private PerceptronLayout _layout;

    public const float DurationNoiseSigma = 0.5f;
    public const float HeadingNoiseSigma  = 0.6f;
    public const float TauEpsilon = 1e-4f;

    public int[] LayerSizes { get; private set; }
    public PerceptronLayout Layout => _layout;
    public int WeightVersion { get; private set; }

    private SharedGradientAccumulator _gradScratch;
    private SharedGradientAccumulator _gradAccum;

    public float OptimizerMaxGradNorm = 3f;

    private const float BaseLearningRateReference    = 0.0525f;
    private const float InvBaseLearningRateReference = 1f / BaseLearningRateReference;

    private const int DefaultTruncWindow      = 8;
    private const int DefaultDecisionCapacity = 16;

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize)
        : this(new[] { inputSize, h1, h2, h3, outputSize }) { }

    public DelayedPerceptron(int[] layerSizes)
        : this(layerSizes, GeneticParameters.Default) { }

    public DelayedPerceptron(int[] layerSizes, in GeneticParameters genetics)
    {
        LayerSizes = layerSizes;
        _layout    = new PerceptronLayout(layerSizes, DefaultTruncWindow, DefaultDecisionCapacity, 0);
        InitWeightsAndBiases(in genetics);
        BuildResidualMask();
        InitOptimizerBuffers();
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        LayerSizes = parent.LayerSizes;
        _layout    = parent._layout;
        CloneWeights(parent);
        BuildResidualMask();
        InitOptimizerBuffers();
    }

    private void InitOptimizerBuffers()
    {
        _gradScratch = new SharedGradientAccumulator(_layout);
        _gradAccum   = new SharedGradientAccumulator(_layout);
    }

    private void BuildResidualMask()
    {
        int L = _layout.L;
        _residual = new bool[L];
        for (int l = 1; l < L - 1; l++)
            _residual[l] = LayerSizes[l] == LayerSizes[l + 1];
    }

    public bool HasResidual(int layer) => _residual[layer];

    public PerceptronLayout BuildContextLayout(int truncWindow, int decisionCapacity, int auxOutputs)
        => new PerceptronLayout(LayerSizes, truncWindow, decisionCapacity, auxOutputs);

    public PerceptronContext CreateContext(GeneticParameters? p = null,
        int truncWindow = 8, int decisionCapacity = 16, int auxOutputs = 0)
        => new PerceptronContext(BuildContextLayout(truncWindow, decisionCapacity, auxOutputs),
                                 p ?? GeneticParameters.Default);

    public static float DurationFromLogit(float logit, float minDuration, float maxDuration)
    {
        float s = 1f / (1f + MathF.Exp(-logit));
        return minDuration + s * (maxDuration - minDuration);
    }
    public DelayedItem Decide(float[] input, PerceptronContext ctx, int delaySteps,
        ValueCritic critic, float gamma, float dt, float simTime,
        float minDuration, float maxDuration, float epsilon, float[] logitBias)
    {
        ctx.DeltaTime = dt > 0f ? dt : ctx.DeltaTime;

        ref readonly var rules = ref SimulationRules.Frame;
        int ordinal = ctx.NextDecisionOrdinal();

        int slot = ForwardPass(input, ctx, logitBias, out int stamp);

        var lay         = ctx.Layout;
        var outAct      = ctx.Activation(lay.L);
        int actionCount = lay.ActionCount;

        float entropyBoost = 1f + MathF.Max(0f, 1.5f - ctx.AverageEntropy);
        float decay        = MathF.Exp(-ctx.TrainingSteps * rules.EpsilonDecayPerStep);
        float adaptiveEpsilon = MathF.Max(rules.MinEpsilon,
            epsilon * rules.ExplorationEpsilonScale * entropyBoost * decay);
        if (adaptiveEpsilon > 1f) adaptiveEpsilon = 1f;

        bool isExploration = RavineRandom.RangeFloat() < adaptiveEpsilon;

        var behaviour = logitBias != null ? ctx.BiasedProbs : outAct;

        int pred = isExploration
            ? RavineRandom.RangeInt(0, actionCount)
            : RouletteWheelSelection(behaviour, actionCount);

        float entropy      = CalculateOutputEntropy(outAct, actionCount);
        ctx.AverageEntropy = ctx.AverageEntropy * (1f - ctx.Params.EntropyAlpha)
                           + entropy * ctx.Params.EntropyAlpha;
        ctx.Diagnostics.RecordEntropy(entropy);

        if (ctx.Decisions.Count >= ctx.Decisions.Capacity)
            FlushOldest(ctx, input, critic, gamma);

        float pBehaviour = (1f - adaptiveEpsilon) * behaviour[pred]
                         + adaptiveEpsilon / actionCount;

        var item = ctx.Decisions.Push(ctx.NextDecisionId());
        item.Evaluation         = 0f;
        item.CreatedOrdinal     = ordinal;
        item.Predicted          = pred;
        item.StartTime          = simTime;
        item.ValueEstimate      = critic.Predict(input);
        item.ExplorationEpsilon = adaptiveEpsilon;
        item.LogProbability     = MathF.Log(MathF.Max(pBehaviour, 1e-8f));
        item.BpttSlot           = slot;
        item.BpttStamp          = stamp;
        item.WeightVersion      = WeightVersion;
        Array.Copy(input, item.State, input.Length);
        outAct.Slice(0, actionCount).CopyTo(item.Probs);

        float baseLogit = outAct[lay.DurationIndex];
        float noise     = SampleGaussian() * DurationNoiseSigma;

        item.DurationLogit = baseLogit;
        item.DurationNoise = noise;
        item.Duration      = DurationFromLogit(baseLogit + noise, minDuration, maxDuration);

        if (lay.AuxOutputs >= 2)
        {
            float ns = SampleGaussian() * HeadingNoiseSigma;
            float nc = SampleGaussian() * HeadingNoiseSigma;

            float s = MathF.Tanh(outAct[lay.HeadingIndex]     + ns);
            float c = MathF.Tanh(outAct[lay.HeadingIndex + 1] + nc);

            float len = MathF.Sqrt(s * s + c * c);
            if (len < 1e-4f) { s = 0f; c = 1f; len = 1f; }

            float invLen = 1f / len;
            item.HeadingNoiseS = ns;
            item.HeadingNoiseC = nc;
            item.HeadingSin    = s * invLen;
            item.HeadingCos    = c * invLen;
        }

        ctx.Diagnostics.RecordDecision(item.Duration);

        while (ctx.Decisions.Count > delaySteps)
            FlushOldest(ctx, input, critic, gamma);

        return item;
    }

    public void Train(DelayedItem ticket, float advantage, PerceptronContext ctx)
    {
        if (!float.IsFinite(advantage))
        {
            ctx.Diagnostics.RecordNonFiniteGradient(1);
            return;
        }

        int steps = ResolveBpttSteps(ticket, ctx);
        if (steps == 0) return;

        ctx.TrainingSteps++;

        var   lay     = ctx.Layout;
        int   L       = lay.L;
        int   history = lay.HistoryDepth;
        float dt      = ctx.DeltaTime;

        int   actionCount = lay.ActionCount;
        float invN        = 1f / actionCount;
        float entReg      = ctx.Params.EntropyRegularization;
        float invTemp     = 1f / MathF.Max(ctx.Params.SoftmaxTemperature, 1e-3f);
        int   pred        = ticket.Predicted;

        float clipEps = SimulationRules.Frame.PpoClipEpsilon;

        ReadOnlySpan<float> probs;
        if (ticket.WeightVersion == WeightVersion)
            probs = ticket.Probs.AsSpan(0, actionCount);
        else
        {
            EvaluatePolicy(ticket, ctx);
            probs = ctx.EvalProbs;
        }

        float eps   = ticket.ExplorationEpsilon;
        float pPure = probs[pred];
        float pMix  = (1f - eps) * pPure + eps * invN;

        float ratio = MathF.Exp(MathF.Log(MathF.Max(pMix, 1e-8f)) - ticket.LogProbability);
        if (!float.IsFinite(ratio)) ratio = 1f;
        float mixScale = pMix > 1e-8f ? (1f - eps) * pPure / pMix : 1f;

        bool clipped = (advantage > 0f && ratio > 1f + clipEps)
                    || (advantage < 0f && ratio < 1f - clipEps);

        float gate = clipped ? 0f : ratio * advantage;

        if (clipped && entReg <= 0f) return;

        float policyGate = gate * mixScale;
        var   outErr     = ctx.OutErrBuf;

        for (int i = 0; i < actionCount; i++)
        {
            float oneHot = i == pred ? 1f : 0f;
            float p      = probs[i];
            outErr[i] = policyGate * (oneHot - p) * invTemp + entReg * (invN - p);
        }

        outErr[lay.DurationIndex] =
            gate * ticket.DurationNoise / (DurationNoiseSigma * DurationNoiseSigma);

        if (lay.AuxOutputs >= 2)
        {
            float invHeadVar = 1f / (HeadingNoiseSigma * HeadingNoiseSigma);
            outErr[lay.HeadingIndex]     = gate * ticket.HeadingNoiseS * invHeadVar;
            outErr[lay.HeadingIndex + 1] = gate * ticket.HeadingNoiseC * invHeadVar;
        }

        var g = _gradScratch;
        g.Clear();

        ctx.ClearWorkingDeltas();

        int nonFinite = 0;

        for (int step = 0; step < steps; step++)
        {
            int t = ticket.BpttSlot - step;
            if (t < 0) t += history;

            if (step == 0)
            {
                var wDHLast = ctx.Working(L - 1);
                for (int i = 0; i < lay.OutputSize; i++)
                    wDHLast[i] += outErr[i];
            }

            for (int l = L - 1; l >= 0; l--)
            {
                var prevActs = ctx.SlotPrevActs(t, l);
                var hBef     = ctx.SlotHBefore(t, l);
                var fArr     = ctx.SlotF(t, l);
                var tauArr   = ctx.SlotTau(t, l);
                var aArr     = ctx.SlotA(t, l);
                var wDH      = ctx.Working(l);
                var tempDH   = ctx.Temporal(l);

                bool hasPrev = l > 0;
                var  prevWDH = hasPrev ? ctx.Working(l - 1) : default;

                tempDH.Clear();

                if (_residual[l] && hasPrev)
                    for (int i = 0; i < wDH.Length; i++)
                        prevWDH[i] += wDH[i];

                int inputs = lay.LayerSizes[l];
                int rowLen = inputs << 1;

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

                    int wi = lay.RowIndex(l, n);
                    var wRow = _wt.AsSpan(wi, rowLen);
                    var gRow = g.WT.AsSpan(wi, rowLen);
                    g.MarkTouched(l, n);

                    if (hasPrev)
                    {
                        for (int i = 0; i < inputs; i++)
                        {
                            int k = i << 1;
                            float a = prevActs[i];

                            float back = dPreF * wRow[k] + dPreT * wRow[k | 1];
                            if (float.IsFinite(back)) prevWDH[i] += back;
                            else nonFinite++;

                            gRow[k]     += dPreF * a;
                            gRow[k | 1] += dPreT * a;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < inputs; i++)
                        {
                            int k = i << 1;
                            float a = prevActs[i];
                            gRow[k]     += dPreF * a;
                            gRow[k | 1] += dPreT * a;
                        }
                    }

                    int bi = lay.BiasIndex(l, n);
                    g.BT[bi]     += dPreF;
                    g.BT[bi | 1] += dPreT;
                }
            }

            ctx.SwapDeltaBuffers();
        }

        float norm = (float)Math.Sqrt(g.SquaredNorm());

        if (!float.IsFinite(norm))
        {
            ctx.Diagnostics.RecordNonFiniteGradient(nonFinite + 1);
            return;
        }

        ctx.Diagnostics.RecordGradientNorm(norm);

        float clipNorm = MathF.Min(MathF.Max(ctx.Params.MaxGradientNorm, 1e-3f), OptimizerMaxGradNorm);
        float scale    = MathF.Min(1f, clipNorm / (norm + 1e-8f));
        float lrMul    = ctx.Params.BaseLearningRate * InvBaseLearningRateReference;

        _gradAccum.AddScaled(g, scale * lrMul);

        if (nonFinite > 0) ctx.Diagnostics.RecordNonFiniteGradient(nonFinite);
    }

    private static void RowDot(ReadOnlySpan<float> row, ReadOnlySpan<float> inp, int inputs,
        float biasF, float biasTau, out float preF, out float preTau)
    {
        var w4 = MemoryMarshal.Cast<float, float4>(row);

        float4 acc0 = float4.zero;
        float4 acc1 = float4.zero;

        int pairs = inputs >> 1;
        int p = 0, i = 0;

        for (; p + 1 < pairs; p += 2, i += 4)
        {
            float a0 = inp[i],     a1 = inp[i + 1];
            float a2 = inp[i + 2], a3 = inp[i + 3];
            acc0 += w4[p]     * new float4(a0, a0, a1, a1);
            acc1 += w4[p + 1] * new float4(a2, a2, a3, a3);
        }

        for (; p < pairs; p++, i += 2)
        {
            float a0 = inp[i], a1 = inp[i + 1];
            acc0 += w4[p] * new float4(a0, a0, a1, a1);
        }

        float4 acc = acc0 + acc1;
        preF   = biasF   + acc.x + acc.z;
        preTau = biasTau + acc.y + acc.w;

        if ((inputs & 1) != 0)
        {
            int k = (inputs - 1) << 1;
            float a = inp[inputs - 1];
            preF   += row[k]     * a;
            preTau += row[k | 1] * a;
        }
    }

    private int ForwardPass(float[] input, PerceptronContext ctx, float[] logitBias, out int stamp)
    {
        var   lay  = ctx.Layout;
        float dt   = ctx.DeltaTime;
        int   slot = ctx.BpttPtr;
        int   L    = lay.L;

        input.AsSpan(0, lay.InputSize).CopyTo(ctx.Activation(0));

        for (int l = 0; l < L; l++)
        {
            var inp = ctx.Activation(l);
            var h   = ctx.Hidden(l);
            var act = ctx.Activation(l + 1);

            inp.CopyTo(ctx.SlotPrevActs(slot, l));
            h.CopyTo(ctx.SlotHBefore(slot, l));

            var fSlot   = ctx.SlotF(slot, l);
            var tauSlot = ctx.SlotTau(slot, l);
            var aSlot   = ctx.SlotA(slot, l);

            bool res    = _residual[l];
            int  inputs = lay.LayerSizes[l];
            int  rowLen = inputs << 1;
            int  neurons = h.Length;

            for (int n = 0; n < neurons; n++)
            {
                int wi = lay.RowIndex(l, n);
                int bi = lay.BiasIndex(l, n);

                RowDot(_wt.AsSpan(wi, rowLen), inp, inputs, _bt[bi], _bt[bi | 1],
                       out float preF, out float preTau);

                float f   = MathF.Tanh(preF);
                float tau = MathF.Max(Softplus(preTau), TauEpsilon);
                float A   = 1f + dt / tau;

                fSlot[n]   = f;
                tauSlot[n] = tau;
                aSlot[n]   = A;

                float hv = (h[n] + dt * f) / A;
                h[n]   = hv;
                act[n] = res ? hv + inp[n] : hv;
            }
        }

        var   outAct      = ctx.Activation(L);
        int   actionCount = lay.ActionCount;
        float temp        = ctx.Params.SoftmaxTemperature;

        if (logitBias != null)
        {
            var biased = ctx.LogitScratch;
            for (int i = 0; i < actionCount; i++) biased[i] = outAct[i] + logitBias[i];
            SoftmaxInPlace(biased, ctx.SoftmaxBuf, actionCount, temp);
            biased.Slice(0, actionCount).CopyTo(ctx.BiasedProbs);
        }

        SoftmaxInPlace(outAct, ctx.SoftmaxBuf, actionCount, temp);

        stamp               = ctx.NextForwardStamp();
        ctx.SlotStamp[slot] = stamp;

        int history = lay.HistoryDepth;
        int next    = slot + 1;
        ctx.BpttPtr = next == history ? 0 : next;
        if (ctx.BpttCount < history) ctx.BpttCount++;

        return slot;
    }

    private void EvaluatePolicy(DelayedItem ticket, PerceptronContext ctx)
    {
        var   lay = ctx.Layout;
        int   L   = lay.L;
        int   t   = ticket.BpttSlot;
        float dt  = ctx.DeltaTime;

        ticket.State.AsSpan(0, lay.InputSize).CopyTo(ctx.EvalActivation(0));

        for (int l = 0; l < L; l++)
        {
            var inp = ctx.EvalActivation(l);
            var h   = ctx.EvalHidden(l);
            var act = ctx.EvalActivation(l + 1);

            ctx.SlotHBefore(t, l).CopyTo(h);

            bool res    = _residual[l];
            int  inputs = lay.LayerSizes[l];
            int  rowLen = inputs << 1;
            int  neurons = h.Length;

            for (int n = 0; n < neurons; n++)
            {
                int wi = lay.RowIndex(l, n);
                int bi = lay.BiasIndex(l, n);

                RowDot(_wt.AsSpan(wi, rowLen), inp, inputs, _bt[bi], _bt[bi | 1],
                       out float preF, out float preTau);

                float f   = MathF.Tanh(preF);
                float tau = MathF.Max(Softplus(preTau), TauEpsilon);
                float A   = 1f + dt / tau;

                float hv = (h[n] + dt * f) / A;
                h[n]   = hv;
                act[n] = res ? hv + inp[n] : hv;
            }
        }

        var outAct = ctx.EvalActivation(L);
        SoftmaxInPlace(outAct, ctx.EvalSoftmax, lay.ActionCount, ctx.Params.SoftmaxTemperature);
        outAct.Slice(0, lay.ActionCount).CopyTo(ctx.EvalProbs);
    }

    private static int ResolveBpttSteps(DelayedItem ticket, PerceptronContext ctx)
    {
        int   slot   = ticket.BpttSlot;
        int   stamp  = ticket.BpttStamp;
        int[] stamps = ctx.SlotStamp;

        if (stamps[slot] != stamp)
        {
            ctx.Diagnostics.RecordStaleSlotDrop();
            return 0;
        }

        int history = ctx.HistoryDepth;
        int max     = Math.Min(ctx.BpttCount, ctx.TruncWindow);
        for (int step = 1; step < max; step++)
        {
            int t = slot - step;
            if (t < 0) t += history;
            if (stamps[t] != stamp - step) return step;
        }
        return max;
    }

    public void ApplyAccumulatedGradients(float lr, float weightDecay, BrainDiagnostics diag)
    {
        var acc = _gradAccum;
        if (acc.Contributions == 0) return;

        float inv = lr / acc.Contributions;
        int   nonFinite = 0;
        int   L = _layout.L;

        for (int l = 0; l < L; l++)
        {
            int neurons = _layout.LayerSizes[l + 1];
            int rowLen  = _layout.LayerSizes[l] << 1;

            for (int n = 0; n < neurons; n++)
            {
                if (!acc.IsTouched(l, n)) continue;

                int wi = _layout.RowIndex(l, n);

                for (int i = 0; i < rowLen; i++)
                {
                    float old = _wt[wi + i];
                    float val = old + acc.WT[wi + i] * inv - weightDecay * old;
                    if (float.IsFinite(val)) _wt[wi + i] = val; else nonFinite++;
                }

                int bi = _layout.BiasIndex(l, n);

                float newBF = _bt[bi]     + acc.BT[bi]     * inv;
                float newBT = _bt[bi | 1] + acc.BT[bi | 1] * inv;

                if (float.IsFinite(newBF)) _bt[bi]     = newBF; else nonFinite++;
                if (float.IsFinite(newBT)) _bt[bi | 1] = newBT; else nonFinite++;
            }
        }

        if (nonFinite > 0) diag?.RecordNonFiniteGradient(nonFinite);
        acc.Clear();
        WeightVersion++;
    }

    private static void SoftmaxInPlace(Span<float> vals, Span<float> buf, int count, float temp)
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

    private int RouletteWheelSelection(ReadOnlySpan<float> probs, int count)
    {
        float pick = RavineRandom.RangeFloat(), cum = 0f;
        for (int i = 0; i < count; i++)
        {
            cum += probs[i];
            if (pick <= cum) return i;
        }
        return count - 1;
    }

    private static float CalculateOutputEntropy(ReadOnlySpan<float> outputs, int count)
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

    private static float DiscountPow(float gamma, int n)
    {
        float g = 1f;
        for (int i = 0; i < n; i++) g *= gamma;
        return g;
    }

    public static float Softplus(float x)
        => x > 20f ? x : MathF.Log(1f + MathF.Exp(x));

    private void InitWeightsAndBiases(in GeneticParameters genetics)
    {
        _wt = new float[_layout.WeightTotal];
        _bt = new float[_layout.BiasTotal];

        int L = _layout.L;
        for (int l = 0; l < L; l++)
        {
            int neurons = _layout.LayerSizes[l + 1];
            int inputs  = _layout.LayerSizes[l];

            float wScale   = MathF.Sqrt(2f / (neurons + inputs));
            float tauScale = 0.1f / MathF.Sqrt(inputs);
            float bRange   = genetics.InitBiasesValues;

            for (int n = 0; n < neurons; n++)
            {
                int wi = _layout.RowIndex(l, n);
                for (int i = 0; i < inputs; i++)
                {
                    float u1 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                    float u2 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                    _wt[wi + (i << 1)]       = MathF.Sqrt(-2f * MathF.Log(u1))
                                             * MathF.Cos(2f * MathF.PI * u2) * wScale;
                    _wt[wi + (i << 1) + 1]   = RavineRandom.RangeFloat(-tauScale, tauScale);
                }

                int bi = _layout.BiasIndex(l, n);
                _bt[bi]     = RavineRandom.RangeFloat(-bRange, bRange);
                _bt[bi | 1] = 0f;
            }
        }
    }

    private void CloneWeights(DelayedPerceptron src)
    {
        _wt = new float[src._wt.Length];
        _bt = new float[src._bt.Length];
        Array.Copy(src._wt, _wt, _wt.Length);
        Array.Copy(src._bt, _bt, _bt.Length);
    }

    public void FlushTerminal(PerceptronContext ctx, ValueCritic critic, float gamma, float penalty)
    {
        var ring  = ctx.Decisions;
        int count = ring.Count;

        for (int i = 0; i < count; i++)
        {
            var item = ring[i];
            if (item.Trained) continue;

            bool  last   = i == count - 1;
            float reward = item.Evaluation + (last ? penalty : 0f);

            float tdTarget;
            if (last)
            {
                item.StepsElapsed = 0;
                tdTarget = reward;
            }
            else
            {
                var next = ring[i + 1];
                int n = next.CreatedOrdinal - item.CreatedOrdinal;
                if (n < 1) n = 1;
                item.StepsElapsed = n;
                tdTarget = reward + DiscountPow(gamma, n) * critic.Predict(next.State);
            }

            float advantage = critic.TrainTD(item.State, tdTarget);

            ctx.Diagnostics.RecordAdvantage(advantage);
            ctx.Diagnostics.RecordCriticError(advantage);

            item.Trained = true;
            if (MathF.Abs(advantage) > 0.05f)
                Train(item, advantage, ctx);
        }

        ring.Clear();
    }

    private void FlushOldest(PerceptronContext ctx, float[] nextState, ValueCritic critic, float gamma)
    {
        var delayed = ctx.Decisions.Oldest;
        if (delayed == null) return;
        ctx.Decisions.PopOldest();
        if (delayed.Trained) return;

        int n = ctx.DecisionOrdinal - delayed.CreatedOrdinal;
        if (n < 1) n = 1;
        delayed.StepsElapsed = n;

        float vNext     = critic.Predict(nextState);
        float tdTarget  = delayed.Evaluation + DiscountPow(gamma, n) * vNext;
        float advantage = critic.TrainTD(delayed.State, tdTarget);

        ctx.Diagnostics.RecordAdvantage(advantage);
        ctx.Diagnostics.RecordCriticError(advantage);

        delayed.Trained = true;
        if (MathF.Abs(advantage) > 0.05f)
            Train(delayed, advantage, ctx);
    }

    public GeneticParameters GetGeneticParameters(PerceptronContext ctx) => ctx.Params;
}