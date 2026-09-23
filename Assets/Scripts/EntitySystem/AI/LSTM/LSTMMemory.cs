using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
                norm = Mathf.Sqrt(norm);
                if (norm < 1e-8f) { rho = 0f; break; }

                float vn = 0f;
                for (int i = 0; i < hiddenSize; i++) vn += v[i] * v[i];
                rho = norm / Mathf.Sqrt(vn);

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
        float scale = Mathf.Sqrt(2f / (rows + cols));
        for (int i = 0; i < weights.Length; i++)
            weights[i] = UnityEngine.Random.Range(-scale, scale);
    }

    public void Dispose()
    {
        if (_wN.IsCreated) _wN.Dispose();
        if (_bN.IsCreated) _bN.Dispose();
    }
}