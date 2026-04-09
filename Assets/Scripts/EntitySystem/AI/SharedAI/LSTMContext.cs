using System;

public class LSTMContext
{
    public readonly float[] H;
    public readonly float[] C;
    public readonly float[] AllGates; // Объединенный массив для F, I, O, CTilde

    public LSTMContext(int inputSize, int hiddenSize)
    {
        H = new float[hiddenSize];
        C = new float[hiddenSize];
        AllGates = new float[4 * hiddenSize];
    }

    public void Reset()
    {
        Array.Clear(H, 0, H.Length);
        Array.Clear(C, 0, C.Length);
    }
}