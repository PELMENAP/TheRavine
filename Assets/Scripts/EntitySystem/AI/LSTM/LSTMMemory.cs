using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public unsafe partial class LSTMMemory : IDisposable
{
    private readonly int inputSize;
    private readonly int hiddenSize;

    private readonly float[] W;
    private readonly float[] b;

    private NativeArray<float> _wN;
    private NativeArray<float> _bN;

    public LSTMMemory(int inputSize, int hiddenSize)
    {
        this.inputSize = inputSize;
        this.hiddenSize = hiddenSize;

        int totalHidden = 4 * hiddenSize;
        int totalCols = inputSize + hiddenSize;

        W = new float[totalHidden * totalCols];
        b = new float[totalHidden];

        InitWeights(W, totalHidden, totalCols);

        for (int i = 0; i < hiddenSize; i++)
            b[i] = 1.0f;
    }

    public LSTMMemory(int inputSize, int hiddenSize, float spectralRadius) : this(inputSize, hiddenSize)
    {
        if (spectralRadius > 0f) ScaleRecurrent(spectralRadius);
    }

    private void ScaleRecurrent(float target)
    {
        int cols = inputSize + hiddenSize;
        var v  = new float[hiddenSize];
        var nv = new float[hiddenSize];

        for (int g = 0; g < 4; g++)
        {
            int rowBase = g * hiddenSize;
            for (int i = 0; i < hiddenSize; i++) v[i] = 1f;

            float rho = 0f;
            for (int it = 0; it < 48; it++)
            {
                float norm = 0f;
                for (int r = 0; r < hiddenSize; r++)
                {
                    int off = (rowBase + r) * cols + inputSize;
                    float sum = 0f;
                    for (int c = 0; c < hiddenSize; c++) sum += W[off + c] * v[c];
                    nv[r] = sum;
                    norm += sum * sum;
                }
                norm = math.sqrt(norm);
                if (norm < 1e-8f) { rho = 0f; break; }

                float vn = 0f;
                for (int i = 0; i < hiddenSize; i++) vn += v[i] * v[i];
                rho = norm / math.sqrt(vn);

                float inv = 1f / norm;
                for (int i = 0; i < hiddenSize; i++) v[i] = nv[i] * inv;
            }

            if (rho < 1e-6f) continue;
            float k = target / rho;
            for (int r = 0; r < hiddenSize; r++)
            {
                int off = (rowBase + r) * cols + inputSize;
                for (int c = 0; c < hiddenSize; c++) W[off + c] *= k;
            }
        }
    }

    public void OrthogonalizeRecurrent(float radius)
    {
        int cols = inputSize + hiddenSize;
        int n    = hiddenSize;
        var q    = new float[n * n];

        for (int g = 0; g < 4; g++)
        {
            for (int r = 0; r < n; r++)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    if (pass == 0)
                    {
                        for (int c = 0; c < n; c++)
                        {
                            float u1 = math.max(UnityEngine.Random.value, 1e-7f);
                            float u2 = UnityEngine.Random.value;
                            q[r * n + c] = math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
                        }
                    }
                    for (int p = 0; p < r; p++)
                    {
                        float dot = 0f;
                        for (int c = 0; c < n; c++) dot += q[r * n + c] * q[p * n + c];
                        for (int c = 0; c < n; c++) q[r * n + c] -= dot * q[p * n + c];
                    }
                }

                float norm = 0f;
                for (int c = 0; c < n; c++) norm += q[r * n + c] * q[r * n + c];
                norm = math.sqrt(norm);
                if (norm < 1e-6f)
                {
                    for (int c = 0; c < n; c++) q[r * n + c] = c == r ? 1f : 0f;
                    norm = 1f;
                }
                float inv = 1f / norm;
                for (int c = 0; c < n; c++) q[r * n + c] *= inv;
            }

            for (int r = 0; r < n; r++)
            {
                int off = (g * n + r) * cols + inputSize;
                for (int c = 0; c < n; c++) W[off + c] = q[r * n + c] * radius;
            }
        }

        if (_wN.IsCreated) _wN.CopyFrom(W);
    }

    public void StepManaged(float[] input, float[] state)
    {
        EnsureNative();
        fixed (float* x = input)
        fixed (float* s = state)
            NeuralKernels.ReservoirStep(WeightsPtr, BiasesPtr, x, s, inputSize, hiddenSize);
    }

    internal int InputSize  => inputSize;
    internal int HiddenSize => hiddenSize;

    public LSTMMemory(LSTMMemory src) : this(src.inputSize, src.hiddenSize)
    {
        Array.Copy(src.W, W, src.W.Length);
        Array.Copy(src.b, b, src.b.Length);
    }

    internal void EnsureNative()
    {
        if (_wN.IsCreated) return;
        if (hiddenSize > NeuralKernels.MaxLstmHidden)
            throw new InvalidOperationException($"LSTM hidden {hiddenSize} > {NeuralKernels.MaxLstmHidden}");

        _wN = new NativeArray<float>(W, Allocator.Persistent);
        _bN = new NativeArray<float>(b, Allocator.Persistent);
    }

    internal float* WeightsPtr => (float*)_wN.GetUnsafeReadOnlyPtr();
    internal float* BiasesPtr  => (float*)_bN.GetUnsafeReadOnlyPtr();

    private void InitWeights(float[] weights, int rows, int cols)
    {
        float scale = math.sqrt(2f / (rows + cols));
        for (int i = 0; i < weights.Length; i++)
            weights[i] = UnityEngine.Random.Range(-scale, scale);
    }

    public void Dispose()
    {
        if (_wN.IsCreated) _wN.Dispose();
        if (_bN.IsCreated) _bN.Dispose();
    }
}