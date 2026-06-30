using UnityEngine;

[CreateAssetMenu(menuName = "Audio/SynthConfig")]
public class SynthConfig : ScriptableObject
{
    [Range(0.01f, 1f)] public float baseVolume = 0.3f;
    [Range(50f, 2000f)] public float baseFrequency = 220f;
    [Range(1, 12)] public int harmonicsCount = 6;
    public WaveformType primaryWaveform = WaveformType.Sine;
    public WaveformType secondaryWaveform = WaveformType.Saw;
    public int sampleRate = 44100;
    [Min(0.01f)] public float duration = 1f;
    [Min(1)] public int hashCacheCapacity = 64;
}