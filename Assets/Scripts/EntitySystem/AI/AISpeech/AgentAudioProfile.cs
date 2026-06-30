public readonly struct AgentAudioProfile
{
    public readonly string GeneticTimbreSeed;
    public readonly int HarmonicsCount;
    public readonly float BaseFrequency;
    public readonly float BaseVolume;
    public readonly float Brightness;
    public readonly float NoiseAmount;
    public readonly float Harmonicity;
    public readonly float Aggression;
    public readonly float Sociality;
    public readonly float Stress;

    public AgentAudioProfile(
        string geneticTimbreSeed, int harmonicsCount,
        float baseFrequency, float baseVolume,
        float brightness, float noiseAmount, float harmonicity,
        float aggression, float sociality, float stress)
    {
        GeneticTimbreSeed = geneticTimbreSeed;
        HarmonicsCount = harmonicsCount;
        BaseFrequency = baseFrequency;
        BaseVolume = baseVolume;
        Brightness = brightness;
        NoiseAmount = noiseAmount;
        Harmonicity = harmonicity;
        Aggression = aggression;
        Sociality = sociality;
        Stress = stress;
    }
}