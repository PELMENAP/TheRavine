using System;

public class PerceptronContext
{
    public readonly float[][] Activations;
    public readonly float[][] HiddenStates;
    public readonly float[][] FVals;
    public readonly float[][] TauVals;
    public readonly float[][] AVals;
    public readonly float[][][] BpttPrevActs;
    public readonly float[][][] BpttHBefore;
    public readonly float[][][] BpttF;
    public readonly float[][][] BpttTau;
    public readonly float[][][] BpttA;
    public int BpttPtr;
    public int BpttCount;

    public readonly float[][] TemporalDeltaH;
    public readonly float[][] WorkingDeltaH;

    public readonly float[] SoftmaxBuf;
    public readonly float[] BiasedProbs;
    public readonly float[] LogitScratch;
    public readonly float[] OutErrBuf;
    public readonly float[] NoisedInputBuf;

    public readonly int ActionCount;
    public readonly int DurationIndex;
    public readonly int HeadingIndex;
    public readonly int AuxOutputs;
    public readonly int OutputSize;

    public readonly DecisionRing     Decisions;
    public readonly BrainDiagnostics Diagnostics = new();

    public GeneticParameters Params;
    public float AverageEntropy;
    public int   TrainingSteps;
    public float DeltaTime = 0.05f;

    private int _nextDecisionId;

    public readonly int[] SlotStamp;
    private int _forwardCounter;

    public readonly float[][] EvalActivations;
    public readonly float[][] EvalHidden;
    public readonly float[]   EvalSoftmaxBuf;
    public readonly float[]   EvalProbs;

    private int _decisionOrdinal;
    public int DecisionOrdinal => _decisionOrdinal;


    public int NextDecisionOrdinal()
    {
        _decisionOrdinal++;
        return _decisionOrdinal;
    }

    public int NextForwardStamp()
    {
        _forwardCounter++;
        if (_forwardCounter == 0) _forwardCounter = 1;
        return _forwardCounter;
    }

    public int NextDecisionId()
    {
        _nextDecisionId++;
        if (_nextDecisionId == 0) _nextDecisionId = 1;
        return _nextDecisionId;
    }

    public readonly int         TruncWindow;
    public readonly int         HistoryDepth;

    private static bool _capacityInvariantReported;

    public PerceptronContext(int[] layerSizes, GeneticParameters p,
        int truncWindow = 8, int decisionCapacity = 16, int auxOutputs = 0)
    {
        if (truncWindow < 1 || decisionCapacity < 2)
        {
            if (!_capacityInvariantReported)
            {
                _capacityInvariantReported = true;
                UnityEngine.Debug.LogWarning(
                    $"PerceptronContext: truncWindow({truncWindow}) / decisionCapacity({decisionCapacity}) out of range, clamped");
            }
            if (truncWindow < 1)      truncWindow = 1;
            if (decisionCapacity < 2) decisionCapacity = 2;
        }

        Params       = p;
        TruncWindow  = truncWindow;
        HistoryDepth = decisionCapacity + truncWindow;
        int L        = layerSizes.Length - 1;

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

        BpttPrevActs = AllocHistorySlots(HistoryDepth, L, layerSizes, true);
        BpttHBefore  = AllocHistorySlots(HistoryDepth, L, layerSizes, false);
        BpttF        = AllocHistorySlots(HistoryDepth, L, layerSizes, false);
        BpttTau      = AllocHistorySlots(HistoryDepth, L, layerSizes, false);
        BpttA        = AllocHistorySlots(HistoryDepth, L, layerSizes, false);

        SlotStamp = new int[HistoryDepth];

        TemporalDeltaH = new float[L][];
        WorkingDeltaH  = new float[L][];
        for (int l = 0; l < L; l++)
        {
            TemporalDeltaH[l] = new float[layerSizes[l + 1]];
            WorkingDeltaH[l]  = new float[layerSizes[l + 1]];
        }

        OutputSize    = layerSizes[layerSizes.Length - 1];
        AuxOutputs    = auxOutputs;
        ActionCount   = OutputSize - 1 - auxOutputs;
        DurationIndex = ActionCount;
        HeadingIndex  = ActionCount + 1;

        if (ActionCount < 2)
            throw new ArgumentException(
                $"PerceptronContext: выходной слой {OutputSize} мал для {auxOutputs} доп. выходов");

        SoftmaxBuf     = new float[ActionCount];
        BiasedProbs    = new float[ActionCount];
        LogitScratch   = new float[ActionCount];
        OutErrBuf      = new float[OutputSize];
        NoisedInputBuf = new float[layerSizes[0]];

        Decisions = new DecisionRing(decisionCapacity, layerSizes[0], ActionCount);

        EvalActivations = new float[layerSizes.Length][];
        for (int i = 0; i < layerSizes.Length; i++)
            EvalActivations[i] = new float[layerSizes[i]];

        EvalHidden = new float[L][];
        for (int l = 0; l < L; l++)
            EvalHidden[l] = new float[layerSizes[l + 1]];

        EvalSoftmaxBuf = new float[ActionCount];
        EvalProbs      = new float[ActionCount];
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
        Array.Clear(SlotStamp, 0, SlotStamp.Length);
        _forwardCounter = 0;
        BpttPtr   = 0;
        BpttCount = 0;
        Decisions.Clear();
    }
}