using System;
using UnityEngine;

public partial class LSTMMemory
{
    private readonly int inputSize;
    private readonly int hiddenSize;

    // Веса объединены в один массив. 
    // Структура: [4 * hiddenSize, inputSize + hiddenSize]
    private readonly float[] W; 
    private readonly float[] b;

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
        {
            b[i] = 1.0f;
        }
    }

    public LSTMMemory(LSTMMemory src) : this(src.inputSize, src.hiddenSize)
    {
        Array.Copy(src.W, this.W, src.W.Length);
        Array.Copy(src.b, this.b, src.b.Length);
    }

    public float[] Step(float[] input, LSTMContext ctx)
    {
        int h = hiddenSize;
        int totalCols = inputSize + h;

        ComputeGates(W, input, ctx.H, b, ctx.AllGates, h, inputSize);

        for (int j = 0; j < h; j++)
        {
            float f = FastSigmoid(ctx.AllGates[j]);           // Forget
            float i = FastSigmoid(ctx.AllGates[j + h]);       // Input
            float o = FastSigmoid(ctx.AllGates[j + 2 * h]);   // Output
            float cTilde = FastTanh(ctx.AllGates[j + 3 * h]); // New Cell

            ctx.C[j] = f * ctx.C[j] + i * cTilde;
            ctx.H[j] = o * FastTanh(ctx.C[j]);
        }

        return ctx.H;
    }

    private static void ComputeGates(float[] w, float[] x, float[] h_prev, float[] bias, float[] res, int h, int xSize)
    {
        int totalCols = xSize + h;
        for (int i = 0; i < 4 * h; i++)
        {
            float sum = bias[i];
            int rowOffset = i * totalCols;

            for (int j = 0; j < xSize; j++)
                sum += w[rowOffset + j] * x[j];

            int hOffset = rowOffset + xSize;
            for (int j = 0; j < h; j++)
                sum += w[hOffset + j] * h_prev[j];

            res[i] = sum;
        }
    }

    private static float FastSigmoid(float x) => 0.5f * (x / (1f + Math.Abs(x))) + 0.5f;
    private static float FastTanh(float x) => x / (1f + Math.Abs(x));

    private void InitWeights(float[] weights, int rows, int cols)
    {
        float scale = Mathf.Sqrt(2f / (rows + cols));
        for (int i = 0; i < weights.Length; i++)
            weights[i] = UnityEngine.Random.Range(-scale, scale);
    }
}