public sealed class ValueCritic
{
    private readonly float[] w;
    private float b;
    private readonly float lr;

    public ValueCritic(int inputSize, float _lr = 0.01f)
    {
        w = new float[inputSize];
        lr = _lr;
    }

    public float Predict(float[] x)
    {
        float sum = b;
        for (int i = 0; i < x.Length; i++) sum += w[i] * x[i];
        return sum;
    }

    public float TrainTD(float[] x, float target)
    {
        float error = target - Predict(x);
        b += lr * error;
        for (int i = 0; i < x.Length; i++)
            w[i] += lr * error * x[i];
        return error;
    }
}