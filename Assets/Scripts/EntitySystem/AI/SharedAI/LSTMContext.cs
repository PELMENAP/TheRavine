using System;

public class LSTMContext
{
    public readonly float[] H;
    public readonly float[] C;
    public readonly float[] Xh;
    public readonly float[] F, I, O, CTilde, Tmp;

    public LSTMContext(int inputSize, int hiddenSize)
    {
        H      = new float[hiddenSize];
        C      = new float[hiddenSize];
        Xh     = new float[inputSize + hiddenSize];
        F      = new float[hiddenSize];
        I      = new float[hiddenSize];
        O      = new float[hiddenSize];
        CTilde = new float[hiddenSize];
        Tmp    = new float[hiddenSize];
    }

    public void Reset()
    {
        Array.Clear(H, 0, H.Length);
        Array.Clear(C, 0, C.Length);
    }
}