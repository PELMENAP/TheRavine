using System;
using System.Runtime.CompilerServices;

public sealed class PerceptronContext
{
    public readonly PerceptronLayout Layout;

    private readonly float[] _buf;
    private int _deltaWork;
    private int _deltaTemp;

    public int BpttPtr;
    public int BpttCount;
    public readonly int[] SlotStamp;

    public readonly DecisionRing     Decisions;
    public readonly BrainDiagnostics Diagnostics = new();

    public GeneticParameters Params;
    public float AverageEntropy;
    public int   TrainingSteps;
    public float DeltaTime = 0.05f;

    private int _nextDecisionId;
    private int _forwardCounter;
    private int _decisionOrdinal;

    public int ActionCount   => Layout.ActionCount;
    public int OutputSize    => Layout.OutputSize;
    public int AuxOutputs    => Layout.AuxOutputs;
    public int DurationIndex => Layout.DurationIndex;
    public int HeadingIndex  => Layout.HeadingIndex;
    public int TruncWindow   => Layout.TruncWindow;
    public int HistoryDepth  => Layout.HistoryDepth;
    public int DecisionOrdinal => _decisionOrdinal;

    public float[] RawBuffer => _buf;
    public int     RawStride => Layout.Stride;

    public PerceptronContext(PerceptronLayout layout, GeneticParameters p)
    {
        Layout = layout;
        Params = p;

        _buf       = new float[layout.Stride];
        SlotStamp  = new int[layout.HistoryDepth];
        _deltaWork = layout.DeltaBaseA;
        _deltaTemp = layout.DeltaBaseB;

        Decisions = new DecisionRing(layout.DecisionCapacity, layout.InputSize, layout.ActionCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Activation(int l) => _buf.AsSpan(Layout.ActOffset[l], Layout.LayerSizes[l]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Hidden(int l) => _buf.AsSpan(Layout.HidOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> EvalActivation(int l) => _buf.AsSpan(Layout.EvalActOffset[l], Layout.LayerSizes[l]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> EvalHidden(int l) => _buf.AsSpan(Layout.EvalHidOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Working(int l) => _buf.AsSpan(_deltaWork + Layout.DeltaOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Temporal(int l) => _buf.AsSpan(_deltaTemp + Layout.DeltaOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotPrevActs(int slot, int l)
        => _buf.AsSpan(Layout.SlotBase(slot) + Layout.SlotActOffset[l], Layout.LayerSizes[l]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotHBefore(int slot, int l)
        => _buf.AsSpan(Layout.SlotBase(slot) + Layout.SlotHidOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotF(int slot, int l)
        => _buf.AsSpan(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + Layout.HiddenSum,
                       Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotTau(int slot, int l)
        => _buf.AsSpan(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + 2 * Layout.HiddenSum,
                       Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotA(int slot, int l)
        => _buf.AsSpan(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + 3 * Layout.HiddenSum,
                       Layout.LayerSizes[l + 1]);

    public Span<float> SoftmaxBuf   => _buf.AsSpan(Layout.SoftmaxOffset,      Layout.ActionCount);
    public Span<float> BiasedProbs  => _buf.AsSpan(Layout.BiasedProbsOffset,  Layout.ActionCount);
    public Span<float> LogitScratch => _buf.AsSpan(Layout.LogitScratchOffset, Layout.ActionCount);
    public Span<float> OutErrBuf    => _buf.AsSpan(Layout.OutErrOffset,       Layout.OutputSize);
    public Span<float> EvalSoftmax  => _buf.AsSpan(Layout.EvalSoftmaxOffset,  Layout.ActionCount);
    public Span<float> EvalProbs    => _buf.AsSpan(Layout.EvalProbsOffset,    Layout.ActionCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SwapDeltaBuffers()
    {
        int t = _deltaWork;
        _deltaWork = _deltaTemp;
        _deltaTemp = t;
    }

    public void ClearWorkingDeltas()
        => Array.Clear(_buf, _deltaWork, Layout.HiddenSum);

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

    public void ResetHiddenStates()
    {
        Array.Clear(_buf, Layout.HidOffset[0], Layout.HiddenSum);
        Array.Clear(SlotStamp, 0, SlotStamp.Length);
        _forwardCounter = 0;
        BpttPtr   = 0;
        BpttCount = 0;
        Decisions.Clear();
    }
}