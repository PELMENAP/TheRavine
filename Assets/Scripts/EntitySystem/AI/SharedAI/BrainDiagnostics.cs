public sealed class BrainDiagnostics
{
    private const float Alpha = 0.05f;

    public float AverageAdvantage { get; private set; }
    public float PositiveAdvantageRatio { get; private set; }
    public float NegativeAdvantageRatio { get; private set; }
    public float AverageActionDuration { get; private set; }
    public float AverageActionCompletionTime { get; private set; }
    public float ActionInterruptionRate { get; private set; }
    public float RewardToDecisionLatency { get; private set; }
    public float PolicyEntropy { get; private set; }
    public float GradientNorm { get; private set; }
    public float CriticError { get; private set; }

    public int AdvantageSamples { get; private set; }
    public int PositiveAdvantageCount { get; private set; }
    public int NegativeAdvantageCount { get; private set; }
    public int DecisionCount { get; private set; }
    public int CompletionCount { get; private set; }
    public int InterruptionCount { get; private set; }

    public int NonFiniteGradientDrops { get; private set; }
    public int StaleSlotDrops { get; private set; }

    public void RecordStaleSlotDrop(int count = 1) => StaleSlotDrops += count;

    public void RecordNonFiniteGradient(int count = 1) => NonFiniteGradientDrops += count;

    public void RecordAdvantage(float advantage)
    {
        AdvantageSamples++;
        if (advantage > 0f) PositiveAdvantageCount++;
        else if (advantage < 0f) NegativeAdvantageCount++;

        AverageAdvantage += (advantage - AverageAdvantage) * Alpha;
        float inv = 1f / AdvantageSamples;
        PositiveAdvantageRatio = PositiveAdvantageCount * inv;
        NegativeAdvantageRatio = NegativeAdvantageCount * inv;
    }

    public void RecordCriticError(float error) =>
        CriticError += (System.MathF.Abs(error) - CriticError) * Alpha;

    public void RecordGradientNorm(float norm) =>
        GradientNorm += (norm - GradientNorm) * Alpha;

    public void RecordEntropy(float entropy) =>
        PolicyEntropy += (entropy - PolicyEntropy) * Alpha;

    public void RecordDecision(float duration)
    {
        DecisionCount++;
        AverageActionDuration += (duration - AverageActionDuration) * Alpha;
    }

    public void RecordCompletion(float elapsed, bool interrupted)
    {
        CompletionCount++;
        if (interrupted) InterruptionCount++;
        AverageActionCompletionTime += (elapsed - AverageActionCompletionTime) * Alpha;
        ActionInterruptionRate = (float)InterruptionCount / CompletionCount;
    }

    public void RecordRewardLatency(float latency) =>
        RewardToDecisionLatency += (latency - RewardToDecisionLatency) * Alpha;

    public void Reset()
    {
        AverageAdvantage = PositiveAdvantageRatio = NegativeAdvantageRatio = 0f;
        AverageActionDuration = AverageActionCompletionTime = 0f;
        ActionInterruptionRate = RewardToDecisionLatency = 0f;
        PolicyEntropy = GradientNorm = CriticError = 0f;
        AdvantageSamples = PositiveAdvantageCount = NegativeAdvantageCount = 0;
        DecisionCount = CompletionCount = InterruptionCount = 0;
        NonFiniteGradientDrops = 0;
    }
}