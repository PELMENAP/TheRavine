using System.Runtime.CompilerServices;

public sealed class PerceptronLayout
{
    public readonly int[] LayerSizes;
    public readonly int   L;
    public readonly int   InputSize;
    public readonly int   OutputSize;
    public readonly int   ActionCount;
    public readonly int   AuxOutputs;
    public readonly int   DurationIndex;
    public readonly int   HeadingIndex;
    public readonly int   TruncWindow;
    public readonly int   HistoryDepth;
    public readonly int   DecisionCapacity;

    public readonly int[] ActOffset;
    public readonly int[] HidOffset;
    public readonly int[] EvalActOffset;
    public readonly int[] EvalHidOffset;
    public readonly int[] DeltaOffset;
    public readonly int[] SlotActOffset;
    public readonly int[] SlotHidOffset;

    public readonly int HiddenSum;
    public readonly int SlotStride;
    public readonly int BpttBase;
    public readonly int DeltaBaseA;
    public readonly int DeltaBaseB;

    public readonly int SoftmaxOffset;
    public readonly int BiasedProbsOffset;
    public readonly int LogitScratchOffset;
    public readonly int OutErrOffset;
    public readonly int EvalSoftmaxOffset;
    public readonly int EvalProbsOffset;

    public readonly int Stride;

    public readonly int[] WeightRowOffset;
    public readonly int[] BiasRowOffset;
    public readonly int   WeightTotal;
    public readonly int   BiasTotal;

    public PerceptronLayout(int[] layerSizes, int truncWindow, int decisionCapacity, int auxOutputs)
    {
        if (truncWindow < 1) truncWindow = 1;
        if (decisionCapacity < 2) decisionCapacity = 2;

        LayerSizes       = layerSizes;
        L                = layerSizes.Length - 1;
        InputSize        = layerSizes[0];
        OutputSize       = layerSizes[L];
        AuxOutputs       = auxOutputs;
        ActionCount      = OutputSize - 1 - auxOutputs;
        DurationIndex    = ActionCount;
        HeadingIndex     = ActionCount + 1;
        TruncWindow      = truncWindow;
        DecisionCapacity = decisionCapacity;
        HistoryDepth     = decisionCapacity + truncWindow;

        if (ActionCount < 2)
            throw new System.ArgumentException(
                $"PerceptronLayout: выходной слой {OutputSize} мал для {auxOutputs} доп. выходов");

        int actsSum = 0;
        for (int l = 0; l < L; l++) actsSum += layerSizes[l];

        HiddenSum = 0;
        for (int l = 0; l < L; l++) HiddenSum += layerSizes[l + 1];

        int off = 0;

        ActOffset = new int[L + 1];
        for (int i = 0; i <= L; i++) { ActOffset[i] = off; off += layerSizes[i]; }

        HidOffset = new int[L];
        for (int l = 0; l < L; l++) { HidOffset[l] = off; off += layerSizes[l + 1]; }

        DeltaOffset = new int[L];
        DeltaBaseA  = off;
        int rel = 0;
        for (int l = 0; l < L; l++) { DeltaOffset[l] = rel; rel += layerSizes[l + 1]; }
        off += HiddenSum;
        DeltaBaseB = off;
        off += HiddenSum;

        EvalActOffset = new int[L + 1];
        for (int i = 0; i <= L; i++) { EvalActOffset[i] = off; off += layerSizes[i]; }

        EvalHidOffset = new int[L];
        for (int l = 0; l < L; l++) { EvalHidOffset[l] = off; off += layerSizes[l + 1]; }

        SlotActOffset = new int[L];
        SlotHidOffset = new int[L];
        int s = 0;
        for (int l = 0; l < L; l++) { SlotActOffset[l] = s; s += layerSizes[l]; }
        for (int l = 0; l < L; l++) { SlotHidOffset[l] = s; s += layerSizes[l + 1]; }
        SlotStride = actsSum + 4 * HiddenSum;

        BpttBase = off;
        off += HistoryDepth * SlotStride;

        SoftmaxOffset      = off; off += ActionCount;
        BiasedProbsOffset  = off; off += ActionCount;
        LogitScratchOffset = off; off += ActionCount;
        OutErrOffset       = off; off += OutputSize;
        EvalSoftmaxOffset  = off; off += ActionCount;
        EvalProbsOffset    = off; off += ActionCount;

        Stride = off;

        WeightRowOffset = new int[L];
        BiasRowOffset   = new int[L];
        int w = 0, b = 0;
        for (int l = 0; l < L; l++)
        {
            WeightRowOffset[l] = w;
            BiasRowOffset[l]   = b;
            w += layerSizes[l + 1] * layerSizes[l] * 2;
            b += layerSizes[l + 1] * 2;
        }
        WeightTotal = w;
        BiasTotal   = b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RowIndex(int l, int n) => WeightRowOffset[l] + n * (LayerSizes[l] << 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BiasIndex(int l, int n) => BiasRowOffset[l] + (n << 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SlotBase(int slot) => BpttBase + slot * SlotStride;

    public bool Matches(int[] other)
    {
        if (other == null || other.Length != LayerSizes.Length) return false;
        for (int i = 0; i < other.Length; i++)
            if (other[i] != LayerSizes[i]) return false;
        return true;
    }
}