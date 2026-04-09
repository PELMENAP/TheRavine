using System;
using System.Collections.Generic;

public class PerceptronContext
{
    public readonly float[][] Activations;
    public readonly float[][] HiddenStates;
    public readonly float[][] FVals;
    public readonly float[][] TauVals;
    public readonly float[][] AVals;

    public readonly int         TruncWindow;
    public readonly float[][][] BpttPrevActs;   // [slot][layer] → float[layerSizes[l]]
    public readonly float[][][] BpttHBefore;    // [slot][layer] → float[layerSizes[l+1]]
    public readonly float[][][] BpttF;
    public readonly float[][][] BpttTau;
    public readonly float[][][] BpttA;
    public int BpttPtr;
    public int BpttCount;

    public readonly float[][] TemporalDeltaH;   // δh переносимый назад по времени
    public readonly float[][] WorkingDeltaH;    // temporal + spatial на текущем шаге

    public readonly float[] SoftmaxBuf;         // без new[] в SoftmaxWithTemperature
    public readonly float[] OutErrBuf;          // без new[] в Train
    public readonly float[] NoisedInputBuf;     // резерв, если понадобится

    public readonly List<DelayedItem> DelayedList = new();

    public GeneticParameters Params;
    public float AverageEntropy;
    public int   TrainingSteps;
    public float DeltaTime = 0.05f;

    public PerceptronContext(int[] layerSizes, GeneticParameters p, int truncWindow = 8)
    {
        Params      = p;
        TruncWindow = truncWindow;
        int L       = layerSizes.Length - 1;

        Activations  = new float[layerSizes.Length][];
        HiddenStates = new float[L][];
        FVals        = new float[L][];
        TauVals      = new float[L][];
        AVals        = new float[L][];

        for (int i = 0; i < layerSizes.Length; i++)
            Activations[i] = new float[layerSizes[i]];
        for (int l = 0; l < L; l++)
        {
            int sz          = layerSizes[l + 1];
            HiddenStates[l] = new float[sz];
            FVals[l]        = new float[sz];
            TauVals[l]      = new float[sz];
            AVals[l]        = new float[sz];
        }

        BpttPrevActs = AllocHistorySlots(truncWindow, L, layerSizes, inputSide: true);
        BpttHBefore  = AllocHistorySlots(truncWindow, L, layerSizes, inputSide: false);
        BpttF        = AllocHistorySlots(truncWindow, L, layerSizes, inputSide: false);
        BpttTau      = AllocHistorySlots(truncWindow, L, layerSizes, inputSide: false);
        BpttA        = AllocHistorySlots(truncWindow, L, layerSizes, inputSide: false);

        TemporalDeltaH = new float[L][];
        WorkingDeltaH  = new float[L][];
        for (int l = 0; l < L; l++)
        {
            TemporalDeltaH[l] = new float[layerSizes[l + 1]];
            WorkingDeltaH[l]  = new float[layerSizes[l + 1]];
        }

        int outSize    = layerSizes[layerSizes.Length - 1];
        SoftmaxBuf     = new float[outSize];
        OutErrBuf      = new float[outSize];
        NoisedInputBuf = new float[layerSizes[0]];
    }

    private static float[][][] AllocHistorySlots(int w, int L, int[] sizes, bool inputSide)
    {
        var arr = new float[w][][];
        for (int t = 0; t < w; t++)
        {
            arr[t] = new float[L][];
            for (int l = 0; l < L; l++)
                arr[t][l] = new float[inputSide ? sizes[l] : sizes[l + 1]];
        }
        return arr;
    }

    public void ResetHiddenStates()
    {
        foreach (var h in HiddenStates)
            Array.Clear(h, 0, h.Length);
        BpttPtr   = 0;
        BpttCount = 0;
    }
}