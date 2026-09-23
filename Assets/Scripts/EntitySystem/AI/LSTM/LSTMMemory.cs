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