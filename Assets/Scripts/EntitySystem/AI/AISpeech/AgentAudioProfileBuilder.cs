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
        float brightness = math.saturate(energy * 0.7f + timeToBreed * 0.3f);
        float noiseAmount = math.saturate(tension * 0.6f + (1f - health) * 0.4f);
        float harmonicity = math.saturate(1f - tension * 0.5f);

        // age сейчас нигде не участвует в формуле звука — либо подключите его явно
        // (например, в harmonicity или decay), либо уберите параметр из сигнатуры,
        // чтобы не врать вызывающему коду о его влиянии
        _ = age;

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