using UnityEngine;
using System;

public sealed class RandomNetworkDistillation
{
    private const int Hidden    = 32;
    private const int Embedding = 16;
    private const float NormAlpha = 0.01f;

    private readonly float[][] targetW1;
    private readonly float[]   targetB1;
    private readonly float[][] targetW2;

    private readonly float[][] predW1;
    private readonly float[]   predB1;
    private readonly float[][] predW2;
    private readonly float[]   predB2;

    private readonly float[] targetHidden;
    private readonly float[] targetOut;
    private readonly float[] predHidden;
    private readonly float[] predOut;

    private readonly float lr;
    private float runningMeanErr = 1f;

    public RandomNetworkDistillation(int inputSize, float _lr = 0.01f)
    {
        lr = _lr;
        targetW1 = Init(Hidden, inputSize);
        targetB1 = new float[Hidden];
        targetW2 = Init(Embedding, Hidden);

        predW1 = Init(Hidden, inputSize);
        predB1 = new float[Hidden];
        predW2 = Init(Embedding, Hidden);
        predB2 = new float[Embedding];

        targetHidden = new float[Hidden];
        targetOut    = new float[Embedding];
        predHidden   = new float[Hidden];
        predOut      = new float[Embedding];
    }

    public float ComputeIntrinsicReward(float[] x)
    {
        ForwardTarget(x);
        ForwardPredictor(x);

        float sqErr = 0f;
        for (int i = 0; i < Embedding; i++)
        {
            float d = predOut[i] - targetOut[i];
            sqErr += d * d;
        }
        sqErr /= Embedding;

        TrainPredictor(x);

        runningMeanErr += NormAlpha * (sqErr - runningMeanErr);
        return Mathf.Clamp01(sqErr / Mathf.Max(runningMeanErr, 1e-4f));
    }

    private void ForwardTarget(float[] x)
    {
        for (int i = 0; i < Hidden; i++)
        {
            float sum = targetB1[i];
            var row = targetW1[i];
            for (int j = 0; j < x.Length; j++) sum += row[j] * x[j];
            targetHidden[i] = MathF.Tanh(sum);
        }
        for (int i = 0; i < Embedding; i++)
        {
            float sum = 0f;
            var row = targetW2[i];
            for (int j = 0; j < Hidden; j++) sum += row[j] * targetHidden[j];
            targetOut[i] = sum;
        }
    }

    private readonly float[] predHiddenPre = new float[Hidden];
    private void ForwardPredictor(float[] x)
    {
        for (int i = 0; i < Hidden; i++)
        {
            float sum = predB1[i];
            var row = predW1[i];
            for (int j = 0; j < x.Length; j++) sum += row[j] * x[j];
            predHiddenPre[i] = sum;
            predHidden[i] = MathF.Tanh(sum);
        }
        for (int i = 0; i < Embedding; i++)
        {
            float sum = predB2[i];
            var row = predW2[i];
            for (int j = 0; j < Hidden; j++) sum += row[j] * predHidden[j];
            predOut[i] = sum;
        }
    }

    private void TrainPredictor(float[] x)
    {
        Span<float> deltaHidden = stackalloc float[Hidden];

        for (int i = 0; i < Embedding; i++)
        {
            float d = predOut[i] - targetOut[i];
            var row = predW2[i];
            for (int j = 0; j < Hidden; j++)
                deltaHidden[j] += d * row[j];

            predB2[i] -= lr * d;
            for (int j = 0; j < Hidden; j++)
                row[j] -= lr * d * predHidden[j];
        }

        for (int i = 0; i < Hidden; i++)
        {
            float dTanh = deltaHidden[i] * (1f - predHidden[i] * predHidden[i]);
            predB1[i] -= lr * dTanh;
            var row = predW1[i];
            for (int j = 0; j < x.Length; j++)
                row[j] -= lr * dTanh * x[j];
        }
    }

    private static float[][] Init(int rows, int cols)
    {
        var w = new float[rows][];
        float scale = Mathf.Sqrt(2f / (rows + cols));
        for (int i = 0; i < rows; i++)
        {
            w[i] = new float[cols];
            for (int j = 0; j < cols; j++)
                w[i][j] = UnityEngine.Random.Range(-scale, scale);
        }
        return w;
    }
}