using System;
using UnityEngine;
using TheRavine.Extensions;


public partial class LSTMMemory
{
    private readonly int inputSize;
    private readonly int hiddenSize;

    private readonly float[] h; // скрытое состояние
    private readonly float[] c; // ячейка памяти
    private readonly float[,] Wf, Wi, Wo, Wc;
    private readonly float[] bf, bi, bo, bc;

    private readonly float[] xh; 
    private readonly float[] f, i, o;
    private readonly float[] cTilde;
    private readonly float[] tmp;

    private readonly float[] dh, dc;

    public LSTMMemory(int inputSize, int hiddenSize)
    {
        this.inputSize = inputSize;
        this.hiddenSize = hiddenSize;

        h = new float[hiddenSize];
        c = new float[hiddenSize];

        Wf = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wi = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wo = InitWeights(hiddenSize, inputSize + hiddenSize);
        Wc = InitWeights(hiddenSize, inputSize + hiddenSize);

        bf = new float[hiddenSize];
        bi = new float[hiddenSize];
        bo = new float[hiddenSize];
        bc = new float[hiddenSize];

        xh = new float[inputSize + hiddenSize];
        f = new float[hiddenSize];
        i = new float[hiddenSize];
        o = new float[hiddenSize];
        cTilde = new float[hiddenSize];
        tmp = new float[hiddenSize];
    }

    private float[,] InitWeights(int rows, int cols)
    {
        var w = new float[rows, cols];
        float scale = Mathf.Sqrt(2f / (rows + cols));
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                w[i, j] = RavineRandom.RangeFloat(-scale, scale);
        return w;
    }

    private void MatVecMul(float[,] W, float[] x, float[] result)
    {
        int rows = W.GetLength(0);
        int cols = W.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            float sum = 0f;
            for (int j = 0; j < cols; j += 4)
            {
                if (j + 3 < cols)
                {
                    sum += W[i, j] * x[j] + 
                        W[i, j+1] * x[j+1] + 
                        W[i, j+2] * x[j+2] + 
                        W[i, j+3] * x[j+3];
                }
                else
                {
                    for (int k = j; k < cols; k++)
                        sum += W[i, k] * x[k];
                }
            }
            result[i] = sum;
        }
    }

    private void Add(float[] x, float[] bias, float[] result)
    {
        for (int i = 0; i < x.Length; i++)
            result[i] = x[i] + bias[i];
    }

    private void FastSigmoidInPlace(float[] x)
    {
        for (int i = 0; i < x.Length; i++)
            x[i] = 0.5f * (x[i] / (1f + Mathf.Abs(x[i]))) + 0.5f;
    }

    private void FastTanhInPlace(float[] x)
    {
        for (int i = 0; i < x.Length; i++)
            x[i] = x[i] / (1f + Mathf.Abs(x[i]));
    }
    private float FastTanh(float x) => x / (1f + Mathf.Abs(x));

    public float[] Step(float[] input)
    {
        // сформировать xh = [input; h]
        Array.Copy(input, 0, xh, 0, inputSize);
        Array.Copy(h, 0, xh, inputSize, hiddenSize);

        // forget gate
        MatVecMul(Wf, xh, tmp);
        Add(tmp, bf, f);
        FastSigmoidInPlace(f);

        // input gate
        MatVecMul(Wi, xh, tmp);
        Add(tmp, bi, i);
        FastSigmoidInPlace(i);

        // output gate
        MatVecMul(Wo, xh, tmp);
        Add(tmp, bo, o);
        FastSigmoidInPlace(o);

        // candidate
        MatVecMul(Wc, xh, tmp);
        Add(tmp, bc, cTilde);
        FastTanhInPlace(cTilde);

        for (int j = 0; j < hiddenSize; j++)
            c[j] = f[j] * c[j] + i[j] * cTilde[j];

        for (int j = 0; j < hiddenSize; j++)
            h[j] = o[j] * (float)FastTanh(c[j]);

        return h;
    }

    public void Train(float[] input, float[] target, float lr)
    {
        float[] y = Step(input);

        for (int j = 0; j < hiddenSize; j++)
            dh[j] = y[j] - target[j];

        for (int j = 0; j < hiddenSize; j++)
        {
            float tanhC = (float)Math.Tanh(c[j]);
            float doGate = dh[j] * tanhC * o[j] * (1 - o[j]);
            float dcGate = dh[j] * o[j] * (1 - tanhC * tanhC);

            float diGate = dcGate * cTilde[j] * i[j] * (1 - i[j]);
            float dfGate = dcGate * c[j] * f[j] * (1 - f[j]);
            float dcTilde = dcGate * i[j] * (1 - cTilde[j] * cTilde[j]);

            for (int k = 0; k < xh.Length; k++)
            {
                Wf[j, k] -= lr * dfGate * xh[k];
                Wi[j, k] -= lr * diGate * xh[k];
                Wo[j, k] -= lr * doGate * xh[k];
                Wc[j, k] -= lr * dcTilde * xh[k];
            }

            bf[j] -= lr * dfGate;
            bi[j] -= lr * diGate;
            bo[j] -= lr * doGate;
            bc[j] -= lr * dcTilde;
        }
    }


    public void ResetState()
    {
        Array.Clear(h, 0, h.Length);
        Array.Clear(c, 0, c.Length);
    }

    public float[] HiddenState => h;
    public float[] CellState => c;
    public int InputSize => inputSize;
    public int HiddenSize => hiddenSize;
}
