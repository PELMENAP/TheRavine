using System;

public unsafe struct KernelLayout
{
    public const int MaxLayers  = 8;
    public const int MaxActions = 16;

    public int L;
    public int InputSize;
    public int OutputSize;
    public int ActionCount;
    public int AuxOutputs;
    public int DurationIndex;
    public int HeadingIndex;
    public int HiddenSum;
    public int SlotStride;
    public int BpttBase;
    public int HistoryDepth;
    public int SoftmaxOffset;
    public int BiasedProbsOffset;
    public int LogitScratchOffset;
    public int WeightTotal;
    public int BiasTotal;
    public int ActTotal;

    public fixed int  Sizes[MaxLayers + 1];
    public fixed int  ActOffset[MaxLayers + 1];
    public fixed int  HidOffset[MaxLayers];
    public fixed int  NeuronOffset[MaxLayers];
    public fixed int  SlotActOffset[MaxLayers];
    public fixed int  SlotHidOffset[MaxLayers];
    public fixed int  WeightRowOffset[MaxLayers];
    public fixed int  BiasRowOffset[MaxLayers];
    public fixed byte Residual[MaxLayers];

    public int NeuronTotal => HiddenSum;
    public int ParamTotal  => WeightTotal + BiasTotal;
    public int WorkStride  => 3 * HiddenSum + OutputSize + ActTotal + 2 * ActionCount;

    public static KernelLayout Build(PerceptronLayout lay, bool[] residual)
    {
        if (lay.L > MaxLayers)
            throw new ArgumentException($"KernelLayout: {lay.L} слоёв > {MaxLayers}");
        if (lay.ActionCount > MaxActions)
            throw new ArgumentException($"KernelLayout: {lay.ActionCount} действий > {MaxActions}");

        var k = new KernelLayout
        {
            L                  = lay.L,
            InputSize          = lay.InputSize,
            OutputSize         = lay.OutputSize,
            ActionCount        = lay.ActionCount,
            AuxOutputs         = lay.AuxOutputs,
            DurationIndex      = lay.DurationIndex,
            HeadingIndex       = lay.HeadingIndex,
            HiddenSum          = lay.HiddenSum,
            SlotStride         = lay.SlotStride,
            BpttBase           = lay.BpttBase,
            HistoryDepth       = lay.HistoryDepth,
            SoftmaxOffset      = lay.SoftmaxOffset,
            BiasedProbsOffset  = lay.BiasedProbsOffset,
            LogitScratchOffset = lay.LogitScratchOffset,
            WeightTotal        = lay.WeightTotal,
            BiasTotal          = lay.BiasTotal,
            ActTotal           = lay.ActOffset[lay.L] + lay.LayerSizes[lay.L],
        };

        for (int i = 0; i <= lay.L; i++)
        {
            k.Sizes[i]     = lay.LayerSizes[i];
            k.ActOffset[i] = lay.ActOffset[i];
        }

        for (int l = 0; l < lay.L; l++)
        {
            k.HidOffset[l]       = lay.HidOffset[l];
            k.NeuronOffset[l]    = lay.DeltaOffset[l];
            k.SlotActOffset[l]   = lay.SlotActOffset[l];
            k.SlotHidOffset[l]   = lay.SlotHidOffset[l];
            k.WeightRowOffset[l] = lay.WeightRowOffset[l];
            k.BiasRowOffset[l]   = lay.BiasRowOffset[l];
            k.Residual[l]        = residual[l] ? (byte)1 : (byte)0;
        }

        return k;
    }
}