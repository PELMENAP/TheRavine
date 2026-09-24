using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

public unsafe partial class DelayedPerceptron : IDisposable
{
    private float[] _wt;
    private float[] _bt;
    private bool[]  _residual;

    private PerceptronLayout _layout;
    private PerceptronLayout _ctxLayout;

    public const float DurationNoiseSigma = 0.5f;
    public const float AuxNoiseSigma      = 0.6f;
    public const float TauEpsilon = 1e-4f;

    public int[] LayerSizes { get; private set; }
    public PerceptronLayout Layout => _layout;
    public int WeightVersion { get; private set; }
    public long TotalTrainingSteps { get; private set; }

    public float OptimizerMaxGradNorm = 3f;

    private const float BaseLearningRateReference    = 0.0525f;
    private const float InvBaseLearningRateReference = 1f / BaseLearningRateReference;

    private const int DefaultTruncWindow      = 8;
    private const int DefaultDecisionCapacity = 16;

    private NativeArray<float> _wN;
    private NativeArray<float> _bN;
    private bool _nativeStale = true;
    private bool _managedStale;

    private NativeArray<KernelLayout> _kernel;

    private NativeList<TrainTicket>  _tickets;
    private NativeArray<TrainResult> _results;
    private PerceptronContext[]      _ticketOwners = Array.Empty<PerceptronContext>();

    private NativeArray<float> _accum;
    private NativeArray<float> _scratch;
    private NativeArray<float> _work;
    private NativeArray<byte>  _accumTouched;
    private NativeArray<byte>  _scratchTouched;
    private NativeArray<int>   _contrib;
    private NativeArray<int>   _applyNonFinite;
    private int _threads;

    public DelayedPerceptron(int inputSize, int h1, int h2, int h3, int outputSize)
        : this(new[] { inputSize, h1, h2, h3, outputSize }) { }

    public DelayedPerceptron(int[] layerSizes)
    {
        LayerSizes = layerSizes;
        _layout    = new PerceptronLayout(layerSizes, DefaultTruncWindow, DefaultDecisionCapacity, 0);
        InitWeightsAndBiases(SimulationRules.Active.InitBiasRange);
        BuildResidualMask();
    }

    public DelayedPerceptron(DelayedPerceptron parent)
    {
        LayerSizes = parent.LayerSizes;
        _layout    = parent._layout;
        CloneWeights(parent);
        BuildResidualMask();
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
    {
        _ctxLayout = new PerceptronLayout(LayerSizes, truncWindow, decisionCapacity, auxOutputs);
        if (_kernel.IsCreated) _kernel.Dispose();
        return _ctxLayout;
    }

    public PerceptronContext CreateContext(GeneticParameters? p = null,
        int truncWindow = 8, int decisionCapacity = 16, int auxOutputs = 0)
        => new PerceptronContext(new PerceptronLayout(LayerSizes, truncWindow, decisionCapacity, auxOutputs),
                                 p ?? GeneticParameters.Default);

    public static float DurationFromLogit(float logit, float minDuration, float maxDuration)
    {
        float s = 1f / (1f + MathF.Exp(-logit));
        return minDuration + s * (maxDuration - minDuration);
    }

    internal KernelLayout* KernelPtr
    {
        get
        {
            if (!_kernel.IsCreated)
            {
                _kernel = new NativeArray<KernelLayout>(1, Allocator.Persistent);
                _kernel[0] = KernelLayout.Build(_ctxLayout ?? _layout, _residual);
            }
            return (KernelLayout*)_kernel.GetUnsafePtr();
        }
    }

    internal void EnsureNative()
    {
        if (!_wN.IsCreated)
        {
            _wN = new NativeArray<float>(_wt.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _bN = new NativeArray<float>(_bt.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _nativeStale = true;
        }
        if (!_nativeStale) return;

        _wN.CopyFrom(_wt);
        _bN.CopyFrom(_bt);
        _nativeStale  = false;
        _managedStale = false;
    }

    internal float* WeightsPtr => (float*)_wN.GetUnsafePtr();
    internal float* BiasesPtr  => (float*)_bN.GetUnsafePtr();

    private void SyncManaged()
    {
        if (!_managedStale || !_wN.IsCreated) return;
        _wN.CopyTo(_wt);
        _bN.CopyTo(_bt);
        _managedStale = false;
    }

    public int BeginForward(PerceptronContext ctx, float dt)
    {
        if (dt > 0f) ctx.DeltaTime = dt;
        return ctx.BpttPtr;
    }

    public DelayedItem FinishDecide(float[] input, PerceptronContext ctx, int slot, bool biased,
        int delaySteps, ValueCritic critic, float gamma, float simTime,
        float minDuration, float maxDuration, float epsilon, float decay)
    {
        ref readonly var rules = ref SimulationRules.Frame;
        int ordinal = ctx.NextDecisionOrdinal();

        var lay     = ctx.Layout;
        int history = lay.HistoryDepth;

        int stamp = ctx.NextForwardStamp();
        ctx.SlotStamp[slot] = stamp;
        int next = slot + 1;
        ctx.BpttPtr = next == history ? 0 : next;
        if (ctx.BpttCount < history) ctx.BpttCount++;

        var outAct      = ctx.Activation(lay.L);
        int actionCount = lay.ActionCount;

        float entropyBoost = 1f + MathF.Max(0f, 1.5f - ctx.AverageEntropy);
        float adaptiveEpsilon = MathF.Max(rules.MinEpsilon,
            epsilon * rules.ExplorationEpsilonScale * entropyBoost * decay);
        if (adaptiveEpsilon > 1f) adaptiveEpsilon = 1f;

        bool isExploration = RavineRandom.RangeFloat() < adaptiveEpsilon;

        var behaviour = biased ? ctx.BiasedProbs : outAct;

        int pred = isExploration
            ? RavineRandom.RangeInt(0, actionCount)
            : RouletteWheelSelection(behaviour, actionCount);

        float entropy      = CalculateOutputEntropy(outAct, actionCount);
        ctx.AverageEntropy = ctx.AverageEntropy * (1f - ctx.Params.EntropyAlpha)
                           + entropy * ctx.Params.EntropyAlpha;
        ctx.Diagnostics.RecordEntropy(entropy);

        float vNow = critic.Predict(input);

        if (ctx.Decisions.Count >= ctx.Decisions.Capacity)
            FlushOldest(ctx, vNow, critic, gamma, simTime);

        float pBehaviour = (1f - adaptiveEpsilon) * behaviour[pred]
                         + adaptiveEpsilon / actionCount;

        var item = ctx.Decisions.Push(ctx.NextDecisionId());
        item.Evaluation         = 0f;
        item.CreatedOrdinal     = ordinal;
        item.Predicted          = pred;
        item.StartTime          = simTime;
        item.ValueEstimate      = vNow;
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

        int aux = lay.AuxOutputs;
        for (int i = 0; i < aux; i++)
        {
            float n = SampleGaussian() * AuxNoiseSigma;
            item.AuxNoise[i] = n;
            item.AuxValue[i] = MathF.Tanh(outAct[lay.HeadingIndex + i] + n);
        }

        if (aux >= 2)
        {
            float s = item.AuxValue[0];
            float c = item.AuxValue[1];

            float len = MathF.Sqrt(s * s + c * c);
            if (len < 1e-4f) { s = 0f; c = 1f; len = 1f; }

            float invLen = 1f / len;
            item.HeadingSin = s * invLen;
            item.HeadingCos = c * invLen;
        }

        ctx.Diagnostics.RecordDecision(item.Duration);

        while (ctx.Decisions.Count > delaySteps)
            FlushOldest(ctx, vNow, critic, gamma, simTime);

        return item;
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
                tdTarget = reward + DiscountTime(gamma, next.StartTime - item.StartTime)
                                  * critic.Predict(next.State, next.DecisionId);
            }

            float advantage = critic.TrainTD(item.State, tdTarget, item.DecisionId);

            ctx.Diagnostics.RecordAdvantage(advantage);
            ctx.Diagnostics.RecordCriticError(advantage);

            item.Trained = true;
            if (MathF.Abs(advantage) > 0.05f)
                EnqueueTraining(item, advantage, ctx);
        }

        ring.Clear();
    }

    private void FlushOldest(PerceptronContext ctx, float vNext, ValueCritic critic, float gamma, float simTime)
    {
        var delayed = ctx.Decisions.Oldest;
        if (delayed == null) return;
        ctx.Decisions.PopOldest();
        if (delayed.Trained) return;

        int n = ctx.DecisionOrdinal - delayed.CreatedOrdinal;
        if (n < 1) n = 1;
        delayed.StepsElapsed = n;

        float tdTarget  = delayed.Evaluation + DiscountTime(gamma, simTime - delayed.StartTime) * vNext;
        float advantage = critic.TrainTD(delayed.State, tdTarget, delayed.DecisionId);

        ctx.Diagnostics.RecordAdvantage(advantage);
        ctx.Diagnostics.RecordCriticError(advantage);

        delayed.Trained = true;
        if (MathF.Abs(advantage) > 0.05f)
            EnqueueTraining(delayed, advantage, ctx);
    }

    private void EnqueueTraining(DelayedItem item, float advantage, PerceptronContext ctx)
    {
        if (!float.IsFinite(advantage))
        {
            ctx.Diagnostics.RecordNonFiniteGradient(1);
            return;
        }

        int steps = ResolveBpttSteps(item, ctx);
        if (steps == 0) return;

        ctx.TrainingSteps++;
        TotalTrainingSteps++;

        if (!_tickets.IsCreated) _tickets = new NativeList<TrainTicket>(Allocator.Persistent);

        var p = ctx.Params;
        var t = new TrainTicket
        {
            Slot                  = item.BpttSlot,
            Steps                 = steps,
            Pred                  = item.Predicted,
            UseStored             = item.WeightVersion == WeightVersion ? 1 : 0,
            Advantage             = advantage,
            Epsilon               = item.ExplorationEpsilon,
            LogProbability        = item.LogProbability,
            DurationNoise         = item.DurationNoise,
            Dt                    = ctx.DeltaTime,
            Temperature           = p.SoftmaxTemperature,
            EntropyRegularization = p.EntropyRegularization,
            MaxGradNorm           = p.MaxGradientNorm,
            LrMul                 = p.BaseLearningRate * InvBaseLearningRateReference,
        };

        int ac = ctx.Layout.ActionCount;
        for (int i = 0; i < ac; i++) t.Probs[i] = item.Probs[i];
        int aux = ctx.Layout.AuxOutputs;
        for (int i = 0; i < aux; i++) t.AuxNoise[i] = item.AuxNoise[i];

        int idx = _tickets.Length;
        _tickets.Add(t);

        if (idx == _ticketOwners.Length)
            Array.Resize(ref _ticketOwners, Math.Max(_ticketOwners.Length << 1, idx + 1));
        _ticketOwners[idx] = ctx;
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

    private void EnsureTrainingBuffers(KernelLayout* k)
    {
        if (_accum.IsCreated) return;

        _threads = JobsUtility.ThreadIndexCount;

        _accum          = new NativeArray<float>(_threads * k->ParamTotal,  Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _scratch        = new NativeArray<float>(_threads * k->ParamTotal,  Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _work           = new NativeArray<float>(_threads * k->WorkStride,  Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _accumTouched   = new NativeArray<byte>(_threads * k->NeuronTotal,  Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _scratchTouched = new NativeArray<byte>(_threads * k->NeuronTotal,  Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _contrib        = new NativeArray<int>(_threads,                   Allocator.Persistent, NativeArrayOptions.ClearMemory);
        _applyNonFinite = new NativeArray<int>(k->NeuronTotal,             Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    internal JobHandle ScheduleTraining(float clipEps)
    {
        int n = _tickets.IsCreated ? _tickets.Length : 0;
        if (n == 0) return default;

        EnsureNative();
        var k = KernelPtr;
        EnsureTrainingBuffers(k);

        if (!_results.IsCreated || _results.Length < n)
        {
            int cap = Math.Max(_results.IsCreated ? _results.Length << 1 : 0, n);
            if (_results.IsCreated) _results.Dispose();
            _results = new NativeArray<TrainResult>(cap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        var tp = (TrainTicket*)_tickets.GetUnsafePtr();
        for (int i = 0; i < n; i++) tp[i].Ctx = _ticketOwners[i].Ptr;

        return new PerceptronTrainJob
        {
            Tickets              = _tickets.AsArray(),
            Results              = _results,
            Layout               = k,
            W                    = (float*)_wN.GetUnsafeReadOnlyPtr(),
            B                    = (float*)_bN.GetUnsafeReadOnlyPtr(),
            Accum                = (float*)_accum.GetUnsafePtr(),
            Scratch              = (float*)_scratch.GetUnsafePtr(),
            Work                 = (float*)_work.GetUnsafePtr(),
            AccumTouched         = (byte*)_accumTouched.GetUnsafePtr(),
            ScratchTouched       = (byte*)_scratchTouched.GetUnsafePtr(),
            Contrib              = (int*)_contrib.GetUnsafePtr(),
            ParamStride          = k->ParamTotal,
            WorkStride           = k->WorkStride,
            NeuronTotal          = k->NeuronTotal,
            ClipEps              = clipEps,
            OptimizerMaxGradNorm = OptimizerMaxGradNorm,
        }.Schedule(n, 1);
    }

    internal void CompleteTraining()
    {
        int n = _tickets.IsCreated ? _tickets.Length : 0;
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            var r   = _results[i];
            var ctx = _ticketOwners[i];
            _ticketOwners[i] = null;

            if (r.Status == 1) ctx.Diagnostics.RecordGradientNorm(r.Norm);
            if (r.NonFinite > 0) ctx.Diagnostics.RecordNonFiniteGradient(r.NonFinite);
        }

        _tickets.Clear();
    }

    public void ApplyAccumulatedGradients(float lr, float weightDecay, BrainDiagnostics diag)
    {
        if (!_contrib.IsCreated) return;

        int contributions = 0;
        for (int th = 0; th < _threads; th++) contributions += _contrib[th];
        if (contributions == 0) return;

        EnsureNative();
        var k = KernelPtr;
        int neurons = k->NeuronTotal;

        new PerceptronApplyJob
        {
            Layout       = k,
            W            = WeightsPtr,
            B            = BiasesPtr,
            Accum        = (float*)_accum.GetUnsafePtr(),
            AccumTouched = (byte*)_accumTouched.GetUnsafePtr(),
            NonFinite    = (int*)_applyNonFinite.GetUnsafePtr(),
            Threads      = _threads,
            ParamStride  = k->ParamTotal,
            NeuronTotal  = neurons,
            Inv          = lr / contributions,
            WeightDecay  = weightDecay,
        }.Schedule(neurons, 8).Complete();

        int nonFinite = 0;
        for (int i = 0; i < neurons; i++) nonFinite += _applyNonFinite[i];
        for (int th = 0; th < _threads; th++) _contrib[th] = 0;

        if (nonFinite > 0) diag?.RecordNonFiniteGradient(nonFinite);

        WeightVersion++;
        _managedStale = true;
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

    public static float DiscountTime(float gammaPerSecond, float seconds)
        => seconds <= 0f ? 1f : Unity.Mathematics.math.exp(Unity.Mathematics.math.log(gammaPerSecond) * seconds);

    public static float Softplus(float x)
        => x > 20f ? x : MathF.Log(1f + MathF.Exp(x));

    private void InitWeightsAndBiases(float bRange)
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

            for (int n = 0; n < neurons; n++)
            {
                int wi = _layout.RowIndex(l, n);
                for (int i = 0; i < inputs; i++)
                {
                    float u1 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                    float u2 = RavineRandom.RangeFloat(0.0001f, 0.9999f);
                    _wt[wi + (i << 1)]     = MathF.Sqrt(-2f * MathF.Log(u1))
                                           * MathF.Cos(2f * MathF.PI * u2) * wScale;
                    _wt[wi + (i << 1) + 1] = RavineRandom.RangeFloat(-tauScale, tauScale);
                }

                int bi = _layout.BiasIndex(l, n);
                _bt[bi]     = RavineRandom.RangeFloat(-bRange, bRange);
                _bt[bi | 1] = 0f;
            }
        }

        _nativeStale = true;
    }

    private void CloneWeights(DelayedPerceptron src)
    {
        src.SyncManaged();
        _wt = new float[src._wt.Length];
        _bt = new float[src._bt.Length];
        Array.Copy(src._wt, _wt, _wt.Length);
        Array.Copy(src._bt, _bt, _bt.Length);
        _nativeStale = true;
    }

    public GeneticParameters GetGeneticParameters(PerceptronContext ctx) => ctx.Params;

    public void Dispose()
    {
        if (_wN.IsCreated)             _wN.Dispose();
        if (_bN.IsCreated)             _bN.Dispose();
        if (_kernel.IsCreated)         _kernel.Dispose();
        if (_tickets.IsCreated)        _tickets.Dispose();
        if (_results.IsCreated)        _results.Dispose();
        if (_accum.IsCreated)          _accum.Dispose();
        if (_scratch.IsCreated)        _scratch.Dispose();
        if (_work.IsCreated)           _work.Dispose();
        if (_accumTouched.IsCreated)   _accumTouched.Dispose();
        if (_scratchTouched.IsCreated) _scratchTouched.Dispose();
        if (_contrib.IsCreated)        _contrib.Dispose();
        if (_applyNonFinite.IsCreated) _applyNonFinite.Dispose();
        _nativeStale = true;
    }
}