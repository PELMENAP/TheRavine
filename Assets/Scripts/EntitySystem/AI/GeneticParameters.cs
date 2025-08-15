[System.Serializable]
public struct GeneticParameters
{
    public float DefaultEvaluation;
    public float Lambda;
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float SoftmaxTemperature;
    public float EntropyRegularization;
    public float LabelSmoothing;
    public float EntropyAlpha;
    public float InitBiasesValues;
    public float GaussianNoise;
    public float ExplorationPrice;
    public float MutationChance;
    public float BaseDelta;

    public static GeneticParameters Default => new()
    {
        DefaultEvaluation = 0.5f,
        Lambda = 0.005f,
        BaseLearningRate = 0.03f,
        MaxGradientNorm = 1.0f,
        SoftmaxTemperature = 1.8f,
        EntropyRegularization = 0.05f,
        LabelSmoothing = 0.35f,
        EntropyAlpha = 0.1f,
        InitBiasesValues = 0.1f,
        GaussianNoise = 0.03f,
        ExplorationPrice = 0.15f,
        MutationChance = 0.3f,
        BaseDelta = 0.05f
    };
    public static readonly (float min, float max, float mutationScale)[] ParameterRanges = {
        (0.1f, 0.9f, 0.05f),     // DefaultEvaluation
        (0.001f, 0.02f, 0.002f), // Lambda
        (0.005f, 0.1f, 0.05f),   // BaseLearningRate
        (0.5f, 3.0f, 0.2f),      // MaxGradientNorm
        (0.8f, 3.0f, 0.2f),      // SoftmaxTemperature
        (0.01f, 0.2f, 0.05f),    // EntropyRegularization
        (0.1f, 0.5f, 0.05f),     // LabelSmoothing
        (0.05f, 0.3f, 0.05f),    // EntropyAlpha
        (0.01f, 0.3f, 0.05f),    // InitBiasesValues
        (0.01f, 0.1f, 0.01f),    // GaussianNoise
        (0.05f, 0.3f, 0.05f),    // ExplorationPrice
        (0.05f, 0.5f, 0.05f),    // MutationChance
        (0.01f, 0.1f, 0.01f)     // BaseDelta
    };
}