using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class StringToAudioGenerator : MonoBehaviour
{
    [SerializeField] private SynthConfig config;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private string input;

    private AudioClip clip;
    private int clipCapacity;
    private float[] managedSamples;
    private NativeArray<float> samplesBuffer;

    private void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (config) StableHashService.Configure(config.hashCacheCapacity);

        int maxSampleCount = Mathf.CeilToInt(config.sampleRate * config.duration);
        samplesBuffer = new NativeArray<float>(maxSampleCount, Allocator.Persistent);
    }

    private void OnDisable() => ReleaseClip();

    private void OnDestroy()
    {
        ReleaseClip();
        if (samplesBuffer.IsCreated) samplesBuffer.Dispose();
        // GeneticAudioProfileCache не диспозится здесь — он разделяется между агентами
        // и живёт по своему LRU, как StableHashService
    }

    [ContextMenu("Проиграть строку")]
    public void PlayString() => PlayFromStringAsync(input).Forget();

    public async UniTask<AudioClip> PlayFromStringAsync(
        string speech,
        float health = 100, float energy = 100, float danger = 0, float timeToBreed = 0,
        float actionFrequency = 0, float nearestEnemyDist = 0,
        float size = 1, float age = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(speech) || ct.IsCancellationRequested) return null;

        int sampleCount = Mathf.CeilToInt(config.sampleRate * config.duration);
        EnsureClip(sampleCount);
        EnsureManagedBuffer(sampleCount);

        var profile = AgentAudioProfileBuilder.Build(
            health, energy, danger, timeToBreed,
            speech, actionFrequency, nearestEnemyDist, size, age);

        var genetic = GeneticAudioProfileCache.Resolve(
            profile.GeneticTimbreSeed, profile.HarmonicsCount, config.duration);

        var state = AudioParameterMapper.BuildState(genetic, profile);
        
        await AudioSynthesizer.GenerateSamplesToManagedAsync(
            genetic, state, samplesBuffer,
            config.sampleRate, config.duration,
            managedSamples, ct);

        if (ct.IsCancellationRequested || clip == null) return null;

        clip.SetData(managedSamples, 0);
        audioSource.clip = clip;
        audioSource.Play();
        return clip;
    }

    private void EnsureClip(int requiredSamples)
    {
        if (clip != null && requiredSamples <= clipCapacity) return;
        DestroyClip();
        clip = AudioClip.Create("StringAudio", requiredSamples, 1, config.sampleRate, false);
        clipCapacity = requiredSamples;
    }

    private void EnsureManagedBuffer(int requiredSamples)
    {
        if (managedSamples == null || managedSamples.Length < requiredSamples)
            managedSamples = new float[requiredSamples];
    }

    private void ReleaseClip()
    {
        if (audioSource && audioSource.clip == clip) audioSource.Stop();
        DestroyClip();
    }

    private void DestroyClip()
    {
        if (clip == null) return;
        if (Application.isPlaying) Destroy(clip);
        else DestroyImmediate(clip);
        clip = null;
        clipCapacity = 0;
    }
}