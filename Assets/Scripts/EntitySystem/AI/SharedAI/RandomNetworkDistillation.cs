using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public sealed unsafe class RandomNetworkDistillation : IDisposable
{
    public const int Hidden    = 32;
    public const int Embedding = 16;

    public readonly int InputSize;

    private NativeArray<float> _tW1, _tB1, _tW2;
    private NativeArray<float> _pW1, _pB1, _pW2, _pB2;
    private NativeArray<float> _errors;
    private NativeArray<float> _samples;

    private int   _sampleCount;
    private long  _seen;
    private float _runningMeanErr = 1f;

    public RandomNetworkDistillation(int inputSize)
    {
        InputSize = inputSize;

        _tW1 = Init(Hidden * inputSize, Hidden, inputSize);
        _tB1 = new NativeArray<float>(Hidden, Allocator.Persistent);
        _tW2 = Init(Embedding * Hidden, Embedding, Hidden);

        _pW1 = Init(Hidden * inputSize, Hidden, inputSize);
        _pB1 = new NativeArray<float>(Hidden, Allocator.Persistent);
        _pW2 = Init(Embedding * Hidden, Embedding, Hidden);
        _pB2 = new NativeArray<float>(Embedding, Allocator.Persistent);
    }

    public void Collect(float[] input)
    {
        int cap = math.max(1, SimulationRules.Active.RndSamplesPerSweep);
        if (!_samples.IsCreated || _samples.Length != cap * InputSize)
        {
            if (_samples.IsCreated) _samples.Dispose();
            _samples     = new NativeArray<float>(cap * InputSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _sampleCount = 0;
            _seen        = 0;
        }

        _seen++;
        int slot = _sampleCount < cap ? _sampleCount++ : RavineRandom.RangeInt(0, (int)math.min(_seen, int.MaxValue));
        if (slot >= cap) return;
        NativeArray<float>.Copy(input, 0, _samples, slot * InputSize, InputSize);
    }

    public JobHandle ScheduleInfer(NativeArray<float> inputs, NativeArray<int> rows, int count, JobHandle dependency = default)
    {
        if (!_errors.IsCreated || _errors.Length < count)
        {
            if (_errors.IsCreated) _errors.Dispose();
            _errors = new NativeArray<float>(math.max(count, 64), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        return new RndInferJob
        {
            Inputs = inputs, Rows = rows, Errors = _errors, InputSize = InputSize,
            TW1 = _tW1, TB1 = _tB1, TW2 = _tW2,
            PW1 = _pW1, PB1 = _pB1, PW2 = _pW2, PB2 = _pB2,
        }.Schedule(count, 8, dependency);
    }

    public float IntrinsicReward(int index)
    {
        float err = _errors[index];
        _runningMeanErr += SimulationRules.Frame.RndNormAlpha * (err - _runningMeanErr);
        return math.saturate(err / math.max(_runningMeanErr, 1e-4f));
    }

    public void TrainSweep()
    {
        if (_sampleCount == 0 || !_samples.IsCreated) return;

        new RndTrainJob
        {
            Samples = _samples, Count = _sampleCount, InputSize = InputSize,
            Lr = SimulationRules.Active.RndLearningRate,
            TW1 = _tW1, TB1 = _tB1, TW2 = _tW2,
            PW1 = _pW1, PB1 = _pB1, PW2 = _pW2, PB2 = _pB2,
        }.Run();

        _sampleCount = 0;
        _seen        = 0;
    }

    private static NativeArray<float> Init(int length, int rows, int cols)
    {
        var w = new NativeArray<float>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        float scale = math.sqrt(2f / (rows + cols));
        for (int i = 0; i < length; i++) w[i] = RavineRandom.RangeFloat(-scale, scale);
        return w;
    }

    public void Dispose()
    {
        if (_tW1.IsCreated) _tW1.Dispose();
        if (_tB1.IsCreated) _tB1.Dispose();
        if (_tW2.IsCreated) _tW2.Dispose();
        if (_pW1.IsCreated) _pW1.Dispose();
        if (_pB1.IsCreated) _pB1.Dispose();
        if (_pW2.IsCreated) _pW2.Dispose();
        if (_pB2.IsCreated) _pB2.Dispose();
        if (_errors.IsCreated)  _errors.Dispose();
        if (_samples.IsCreated) _samples.Dispose();
    }

    internal static void HiddenLayer(float* w, float* b, float* x, float* h, float* pre, int inputs)
    {
        for (int i = 0; i < Hidden; i++)
        {
            float* row = w + i * inputs;
            float sum = b[i];
            for (int j = 0; j < inputs; j++) sum += row[j] * x[j];
            if (pre != null) pre[i] = sum;
            h[i] = math.tanh(sum);
        }
    }

    internal static float Embed(float* w, float* h, int e)
    {
        float* row = w + e * Hidden;
        float sum = 0f;
        for (int j = 0; j < Hidden; j++) sum += row[j] * h[j];
        return sum;
    }
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public unsafe struct RndInferJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> Inputs;
    [ReadOnly] public NativeArray<int>   Rows;
    [ReadOnly] public NativeArray<float> TW1, TB1, TW2, PW1, PB1, PW2, PB2;
    [WriteOnly] public NativeArray<float> Errors;
    public int InputSize;

    public void Execute(int index)
    {
        const int H = RandomNetworkDistillation.Hidden;
        const int E = RandomNetworkDistillation.Embedding;

        float* x  = (float*)Inputs.GetUnsafeReadOnlyPtr() + Rows[index] * InputSize;
        float* th = stackalloc float[H];
        float* ph = stackalloc float[H];

        RandomNetworkDistillation.HiddenLayer((float*)TW1.GetUnsafeReadOnlyPtr(), (float*)TB1.GetUnsafeReadOnlyPtr(), x, th, null, InputSize);
        RandomNetworkDistillation.HiddenLayer((float*)PW1.GetUnsafeReadOnlyPtr(), (float*)PB1.GetUnsafeReadOnlyPtr(), x, ph, null, InputSize);

        float* tw2 = (float*)TW2.GetUnsafeReadOnlyPtr();
        float* pw2 = (float*)PW2.GetUnsafeReadOnlyPtr();

        float err = 0f;
        for (int e = 0; e < E; e++)
        {
            float d = PB2[e] + RandomNetworkDistillation.Embed(pw2, ph, e) - RandomNetworkDistillation.Embed(tw2, th, e);
            err += d * d;
        }
        Errors[index] = err * (1f / E);
    }
}

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public unsafe struct RndTrainJob : IJob
{
    [ReadOnly] public NativeArray<float> Samples;
    [ReadOnly] public NativeArray<float> TW1, TB1, TW2;
    public NativeArray<float> PW1, PB1, PW2, PB2;
    public int   Count;
    public int   InputSize;
    public float Lr;

    public void Execute()
    {
        const int H = RandomNetworkDistillation.Hidden;
        const int E = RandomNetworkDistillation.Embedding;

        float* th = stackalloc float[H];
        float* ph = stackalloc float[H];
        float* dh = stackalloc float[H];

        float* tw1 = (float*)TW1.GetUnsafeReadOnlyPtr();
        float* tb1 = (float*)TB1.GetUnsafeReadOnlyPtr();
        float* tw2 = (float*)TW2.GetUnsafeReadOnlyPtr();
        float* pw1 = (float*)PW1.GetUnsafePtr();
        float* pb1 = (float*)PB1.GetUnsafePtr();
        float* pw2 = (float*)PW2.GetUnsafePtr();
        float* pb2 = (float*)PB2.GetUnsafePtr();

        for (int s = 0; s < Count; s++)
        {
            float* x = (float*)Samples.GetUnsafeReadOnlyPtr() + s * InputSize;

            RandomNetworkDistillation.HiddenLayer(tw1, tb1, x, th, null, InputSize);
            RandomNetworkDistillation.HiddenLayer(pw1, pb1, x, ph, null, InputSize);
            UnsafeUtility.MemClear(dh, H * sizeof(float));

            for (int e = 0; e < E; e++)
            {
                float d = pb2[e] + RandomNetworkDistillation.Embed(pw2, ph, e) - RandomNetworkDistillation.Embed(tw2, th, e);
                float* row = pw2 + e * H;
                for (int j = 0; j < H; j++)
                {
                    dh[j]  += d * row[j];
                    row[j] -= Lr * d * ph[j];
                }
                pb2[e] -= Lr * d;
            }

            for (int i = 0; i < H; i++)
            {
                float g = dh[i] * (1f - ph[i] * ph[i]);
                pb1[i] -= Lr * g;
                float* row = pw1 + i * InputSize;
                for (int j = 0; j < InputSize; j++) row[j] -= Lr * g * x[j];
            }
        }
    }
}
