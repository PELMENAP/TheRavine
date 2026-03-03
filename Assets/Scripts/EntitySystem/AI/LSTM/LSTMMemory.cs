using System;
using UnityEngine;
using TheRavine.Extensions;

public partial class LSTMMemory
{
    private readonly int inputSize;
    private readonly int hiddenSize;

    // Weights only — state lives in LSTMContext
    private readonly float[,] Wf, Wi, Wo, Wc;
    private readonly float[]  bf, bi, bo, bc;

    public LSTMMemory(int inputSize, int hiddenSize)
    {
        this.inputSize  = inputSize;
        this.hiddenSize = hiddenSize;

        Wf = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wi = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wo = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wc = InitWeights(hiddenSize, inputSize + hiddenSize);

        bf = new float[hiddenSize];
        bi = new float[hiddenSize];
        bo = new float[hiddenSize];
        bc = new float[hiddenSize];
    }

    public LSTMMemory(LSTMMemory src) : this(src.inputSize, src.hiddenSize)
    {
        Array.Copy(src.bf, bf, hiddenSize); Array.Copy(src.bi, bi, hiddenSize);
        Array.Copy(src.bo, bo, hiddenSize); Array.Copy(src.bc, bc, hiddenSize);
        CopyMatrix(src.Wf, Wf); CopyMatrix(src.Wi, Wi);
        CopyMatrix(src.Wo, Wo); CopyMatrix(src.Wc, Wc);
    }

    public float[] Step(float[] input, LSTMContext ctx)
    {
        Array.Copy(input,  0, ctx.Xh, 0,          inputSize);
        Array.Copy(ctx.H,  0, ctx.Xh, inputSize,   hiddenSize);

        MatVecAdd(Wf, ctx.Xh, bf, ctx.F);   FastSigmoidInPlace(ctx.F);
        MatVecAdd(Wi, ctx.Xh, bi, ctx.I);   FastSigmoidInPlace(ctx.I);
        MatVecAdd(Wo, ctx.Xh, bo, ctx.O);   FastSigmoidInPlace(ctx.O);
        MatVecAdd(Wc, ctx.Xh, bc, ctx.CTilde); FastTanhInPlace(ctx.CTilde);

        for (int j = 0; j < hiddenSize; j++)
            ctx.C[j] = ctx.F[j] * ctx.C[j] + ctx.I[j] * ctx.CTilde[j];

        for (int j = 0; j < hiddenSize; j++)
            ctx.H[j] = ctx.O[j] * FastTanh(ctx.C[j]);

        return ctx.H;
    }

    private static void MatVecAdd(float[,] W, float[] x, float[] bias, float[] result)
    {
        int rows = W.GetLength(0);
        int cols = W.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            float sum = bias[i];
            for (int j = 0; j < cols; j += 4)
            {
                if (j + 3 < cols)
                    sum += W[i,j]*x[j] + W[i,j+1]*x[j+1] + W[i,j+2]*x[j+2] + W[i,j+3]*x[j+3];
                else
                    for (int k = j; k < cols; k++) sum += W[i,k]*x[k];
            }
            result[i] = sum;
        }
    }

    private static void FastSigmoidInPlace(float[] x)
    {
        for (int i = 0; i < x.Length; i++)
            x[i] = 0.5f * (x[i] / (1f + Mathf.Abs(x[i]))) + 0.5f;
    }

    private static void FastTanhInPlace(float[] x)
    {
        for (int i = 0; i < x.Length; i++)
            x[i] = x[i] / (1f + Mathf.Abs(x[i]));
    }

    private static float FastTanh(float x) => x / (1f + Mathf.Abs(x));

    private static float[,] InitWeights(int rows, int cols)
    {
        var w     = new float[rows, cols];
        float scale = Mathf.Sqrt(2f / (rows + cols));
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                w[i, j] = RavineRandom.RangeFloat(-scale, scale);
        return w;
    }

    private static void CopyMatrix(float[,] src, float[,] dst)
    {
        int rows = src.GetLength(0), cols = src.GetLength(1);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                dst[i, j] = src[i, j];
    }

    public int InputSize  => inputSize;
    public int HiddenSize => hiddenSize;
}