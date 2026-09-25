using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

public sealed unsafe class PerceptronContext : IDisposable
{
    public readonly PerceptronLayout Layout;

    private readonly FloatSlabPool _pool;
    private int _base;

    public int BpttPtr;
    public int BpttCount;
    public readonly int[] SlotStamp;

    public readonly DecisionRing     Decisions;
    public readonly BrainDiagnostics Diagnostics = new();

    public GeneticParameters Params;
    public float AverageEntropy;
    public int   TrainingSteps;
    public float DeltaTime = 0.05f;
    public float PositiveAdvantageScale = 1f;

    private int _nextDecisionId;
    private int _forwardCounter;
    private int _decisionOrdinal;

    public int ActionCount     => Layout.ActionCount;
    public int OutputSize      => Layout.OutputSize;
    public int AuxOutputs      => Layout.AuxOutputs;
    public int DurationIndex   => Layout.DurationIndex;
    public int HeadingIndex    => Layout.HeadingIndex;
    public int TruncWindow     => Layout.TruncWindow;
    public int HistoryDepth    => Layout.HistoryDepth;
    public int DecisionOrdinal => _decisionOrdinal;
    public bool IsDisposed     => _base < 0;

    public float* Ptr => _pool.Ptr + _base;

    public PerceptronContext(PerceptronLayout layout, GeneticParameters p)
    {
        Layout = layout;
        Params = p;

        _pool = ContextSlabs.Get(layout.Stride);
        _base = _pool.Rent();

        SlotStamp = new int[layout.HistoryDepth];
        Decisions = new DecisionRing(layout.DecisionCapacity, layout.InputSize, layout.ActionCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<float> At(int offset, int length) => new Span<float>(_pool.Ptr + _base + offset, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Activation(int l) => At(Layout.ActOffset[l], Layout.LayerSizes[l]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> Hidden(int l) => At(Layout.HidOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotPrevActs(int slot, int l)
        => At(Layout.SlotBase(slot) + Layout.SlotActOffset[l], Layout.LayerSizes[l]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotHBefore(int slot, int l)
        => At(Layout.SlotBase(slot) + Layout.SlotHidOffset[l], Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotF(int slot, int l)
        => At(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + Layout.HiddenSum, Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotTau(int slot, int l)
        => At(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + 2 * Layout.HiddenSum, Layout.LayerSizes[l + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> SlotA(int slot, int l)
        => At(Layout.SlotBase(slot) + Layout.SlotHidOffset[l] + 3 * Layout.HiddenSum, Layout.LayerSizes[l + 1]);

    public Span<float> SoftmaxBuf   => At(Layout.SoftmaxOffset,      Layout.ActionCount);
    public Span<float> BiasedProbs  => At(Layout.BiasedProbsOffset,  Layout.ActionCount);
    public Span<float> LogitScratch => At(Layout.LogitScratchOffset, Layout.ActionCount);

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
        UnsafeUtility.MemClear(Ptr + Layout.HidOffset[0], (long)Layout.HiddenSum * sizeof(float));
        Array.Clear(SlotStamp, 0, SlotStamp.Length);
        _forwardCounter = 0;
        BpttPtr   = 0;
        BpttCount = 0;
        Decisions.Clear();
    }

    public void Dispose()
    {
        if (_base < 0) return;
        _pool.Return(_base);
        _base = -1;
    }
}