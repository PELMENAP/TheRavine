using System.Collections.Generic;

public class PerceptronContext
{
    public readonly float[][]         Activations;
    public readonly List<DelayedItem> DelayedList = new();
    public GeneticParameters          Params;
    public float                      AverageEntropy;
    public int                        TrainingSteps;

    public PerceptronContext(int[] layerSizes, GeneticParameters p)
    {
        Params      = p;
        Activations = new float[layerSizes.Length][];
        for (int i = 0; i < layerSizes.Length; i++)
            Activations[i] = new float[layerSizes[i]];
    }
}