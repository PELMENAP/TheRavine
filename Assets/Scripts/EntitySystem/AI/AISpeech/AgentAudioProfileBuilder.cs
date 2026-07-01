using Unity.Mathematics;

public static class AgentAudioProfileBuilder
{
    public static AgentAudioProfile Build(
        float health, float energy, float danger, float timeToBreed,
        string speech, float actionFrequency, float nearestEnemyDist,
        float size, float age,
        int harmonicsCount = 6)
    {
        float tension = math.lerp(danger, 1f - nearestEnemyDist, 0.5f);

        float baseFrequency = math.lerp(120f, 480f, 1f - size);
        float baseVolume = math.lerp(0.05f, 0.4f, health);
        
        float ageFactor = math.saturate(age / 100f);
        float brightness = math.saturate(energy * 0.7f + timeToBreed * 0.3f - ageFactor * 0.2f);
        float noiseAmount = math.saturate(tension * 0.6f + (1f - health) * 0.4f + ageFactor * 0.3f);
        
        float harmonicity = math.saturate(1f - tension * 0.5f - ageFactor * 0.1f);

        return new AgentAudioProfile(
            geneticTimbreSeed: speech,
            harmonicsCount: harmonicsCount,
            baseFrequency: baseFrequency,
            baseVolume: baseVolume,
            brightness: brightness,
            noiseAmount: noiseAmount,
            harmonicity: harmonicity,
            aggression: math.saturate(actionFrequency * danger),
            sociality: math.saturate(1f - tension),
            stress: tension
        );
    }
}