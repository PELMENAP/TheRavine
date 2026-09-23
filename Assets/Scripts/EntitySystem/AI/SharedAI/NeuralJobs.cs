using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

public unsafe struct ForwardItem
{
    public float* Ctx;
    public float* Lstm;
    public int    Net;
    public int    Slot;
    public int    InputRow;
    public int    BiasRow;
    public int    Owner;
    public float  Dt;
    public float  Temperature;
}

public unsafe struct NetWeights
{
    public float* W;
    public float* B;
}

public unsafe struct ReservoirItem
{
    public float* State;
    public int    InputRow;
}

public unsafe struct TrainTicket
{
    public float* Ctx;
    public int    Slot;
    public int    Steps;
    public int    Pred;
    public int    UseStored;
    public float  Advantage;
    public float  Epsilon;
    public float  LogProbability;
    public float  DurationNoise;
    public float  HeadingNoiseS;
    public float  HeadingNoiseC;
    public float  Dt;
    public float  Temperature;
    public float  EntropyRegularization;
    public float  MaxGradNorm;
    public float  LrMul;
    public fixed float Probs[KernelLayout.MaxActions];
}

public struct TrainResult
{
    public int   Status;
    public int   NonFinite;
    public float Norm;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public unsafe struct BrainForwardJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<ForwardItem>  Items;
    [ReadOnly] public NativeArray<float>        Inputs;
    [ReadOnly] public NativeArray<float>        Biases;
    [ReadOnly] public NativeArray<KernelLayout> Layouts;
    [ReadOnly] public NativeArray<NetWeights>   Nets;

    public int InputSize;
    public int LstmHidden;
    public int BiasStride;

    public void Execute(int index)
    {
        var it  = Items[index];
        var net = Nets[it.Net];

        KernelLayout* lay = (KernelLayout*)Layouts.GetUnsafeReadOnlyPtr() + it.Net;
        float* input = (float*)Inputs.GetUnsafeReadOnlyPtr() + it.InputRow * InputSize;
        float* bias  = it.BiasRow >= 0
            ? (float*)Biases.GetUnsafeReadOnlyPtr() + it.BiasRow * BiasStride
            : null;

        NeuralKernels.Combine(input, it.Lstm, InputSize, LstmHidden, it.Ctx + lay->ActOffset[0]);

        NeuralKernels.Forward(lay, net.W, net.B, it.Ctx, it.Slot, it.Dt, it.Temperature, bias);
    }
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public unsafe struct ReservoirJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<ReservoirItem> Items;
    [ReadOnly] public NativeArray<float>         Inputs;

    [NativeDisableUnsafePtrRestriction] public float* W;
    [NativeDisableUnsafePtrRestriction] public float* B;

    public int InputSize;
    public int Hidden;

    public void Execute(int index)
    {
        var it = Items[index];
        float* x = (float*)Inputs.GetUnsafeReadOnlyPtr() + it.InputRow * InputSize;
        NeuralKernels.ReservoirStep(W, B, x, it.State, InputSize, Hidden);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Default)]
public unsafe struct PerceptronTrainJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<TrainTicket> Tickets;
    public NativeArray<TrainResult> Results;

    [NativeDisableUnsafePtrRestriction] public KernelLayout* Layout;
    [NativeDisableUnsafePtrRestriction] public float* W;
    [NativeDisableUnsafePtrRestriction] public float* B;
    [NativeDisableUnsafePtrRestriction] public float* Accum;
    [NativeDisableUnsafePtrRestriction] public float* Scratch;
    [NativeDisableUnsafePtrRestriction] public float* Work;
    [NativeDisableUnsafePtrRestriction] public byte*  AccumTouched;
    [NativeDisableUnsafePtrRestriction] public byte*  ScratchTouched;
    [NativeDisableUnsafePtrRestriction] public int*   Contrib;

    public int   ParamStride;
    public int   WorkStride;
    public int   NeuronTotal;
    public float ClipEps;
    public float OptimizerMaxGradNorm;

    [NativeSetThreadIndex] private int _threadIndex;

    public void Execute(int index)
    {
        TrainTicket* tk = (TrainTicket*)Tickets.GetUnsafeReadOnlyPtr() + index;
        int th = _threadIndex;

        float* grad = Scratch + th * ParamStride;
        byte*  gT   = ScratchTouched + th * NeuronTotal;
        float* acc  = Accum + th * ParamStride;
        byte*  aT   = AccumTouched + th * NeuronTotal;
        float* work = Work + th * WorkStride;

        int status = NeuralKernels.Train(tk, Layout, W, B, grad, gT, work, ClipEps, out float norm, out int nf);

        if (status == 1)
        {
            float clipNorm = math.min(math.max(tk->MaxGradNorm, 1e-3f), OptimizerMaxGradNorm);
            float scale    = math.min(1f, clipNorm / (norm + 1e-8f)) * tk->LrMul;
            NeuralKernels.AccumulateAndClear(Layout, grad, gT, acc, aT, scale);
            Contrib[th]++;
        }
        else if (status == 2)
        {
            NeuralKernels.ClearGrad(Layout, grad, gT);
        }

        Results[index] = new TrainResult { Status = status, NonFinite = nf, Norm = norm };
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Default)]
public unsafe struct PerceptronApplyJob : IJobParallelFor
{
    [NativeDisableUnsafePtrRestriction] public KernelLayout* Layout;
    [NativeDisableUnsafePtrRestriction] public float* W;
    [NativeDisableUnsafePtrRestriction] public float* B;
    [NativeDisableUnsafePtrRestriction] public float* Accum;
    [NativeDisableUnsafePtrRestriction] public byte*  AccumTouched;
    [NativeDisableUnsafePtrRestriction] public int*   NonFinite;

    public int   Threads;
    public int   ParamStride;
    public int   NeuronTotal;
    public float Inv;
    public float WeightDecay;

    public void Execute(int nid)
    {
        int L = Layout->L;
        int l = 0;
        while (l + 1 < L && nid >= Layout->NeuronOffset[l + 1]) l++;
        int n = nid - Layout->NeuronOffset[l];

        int* list = stackalloc int[JobsUtility.MaxJobThreadCount];
        int touched = 0;
        for (int th = 0; th < Threads; th++)
            if (AccumTouched[th * NeuronTotal + nid] != 0) list[touched++] = th;

        if (touched == 0)
        {
            NonFinite[nid] = 0;
            return;
        }

        int rowLen = Layout->Sizes[l] << 1;
        int wi     = Layout->WeightRowOffset[l] + n * rowLen;
        int bi     = Layout->BiasRowOffset[l] + (n << 1);
        int wb     = Layout->WeightTotal + bi;
        int nf     = 0;

        for (int k = 0; k < rowLen; k++)
        {
            float sum = 0f;
            for (int j = 0; j < touched; j++) sum += Accum[list[j] * ParamStride + wi + k];

            float old = W[wi + k];
            float val = old + sum * Inv - WeightDecay * old;
            if (math.isfinite(val)) W[wi + k] = val; else nf++;
        }

        for (int k = 0; k < 2; k++)
        {
            float sum = 0f;
            for (int j = 0; j < touched; j++) sum += Accum[list[j] * ParamStride + wb + k];

            float val = B[bi + k] + sum * Inv;
            if (math.isfinite(val)) B[bi + k] = val; else nf++;
        }

        for (int j = 0; j < touched; j++)
        {
            int th = list[j];
            float* row = Accum + th * ParamStride;
            UnsafeUtility.MemClear(row + wi, (long)rowLen * sizeof(float));
            row[wb]     = 0f;
            row[wb + 1] = 0f;
            AccumTouched[th * NeuronTotal + nid] = 0;
        }

        NonFinite[nid] = nf;
    }
}