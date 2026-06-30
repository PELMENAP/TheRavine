using Unity.Mathematics;

public readonly struct GeneticAudioProfile
{
    public readonly StableHashService.HashData BaseHash;
    public readonly int HarmonicsCount;
    public readonly float BaseFrequencyGenetic;
    public readonly float Size;
    public readonly float Harmonicity;

    public GeneticAudioProfile(string seed, int harmonicsCount, float duration, float size, float harmonicity)
    {
        HarmonicsCount = harmonicsCount;
        BaseHash = StableHashService.GetOrCreate(seed, harmonicsCount, duration);
        BaseFrequencyGenetic = math.lerp(120f, 480f, 1f - size);
        Size = size;
        Harmonicity = harmonicity;
    }
}

public struct AudioStateParams
{
    public float Stress, Brightness, Aggression, Sociality, NoiseAmount;
}