using System;
using System.Collections.Generic;

public class PerceptronContext
{
    public readonly float[][] Activations;
    public readonly float[][] HiddenStates;  // персистентное состояние нейронов
    public readonly float[][] FVals;          // tanh(preF) — кеш для backprop
    public readonly float[][] TauVals;        // softplus(preTau) — кеш для backprop
    public readonly float[][] AVals;          // 1 + dt/tau — кеш для backprop
    public readonly List<DelayedItem> DelayedList = new();
    public GeneticParameters Params;
    public float AverageEntropy;
    public int   TrainingSteps;
    public float DeltaTime = 0.05f;


    public PerceptronContext(int[] layerSizes, GeneticParameters p)
    {
        Params   = p;
        int L    = layerSizes.Length - 1;

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
    }

    public void ResetHiddenStates()
    {
        foreach (var h in HiddenStates)
            Array.Clear(h, 0, h.Length);
    }
}