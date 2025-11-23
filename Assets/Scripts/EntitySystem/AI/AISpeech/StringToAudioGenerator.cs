using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using NaughtyAttributes;

public class StringToAudioGenerator : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private int sampleRate = 44100;
    [SerializeField, Min(0.01f)] private float duration = 1f;
    [SerializeField] private AudioSource audioSource;

    [Header("Synthesis")]
    [SerializeField, Range(0.01f, 1f)] private float baseVolume = 0.3f;
    [SerializeField, Range(50f, 2000f)] private float baseFrequency = 220f;
    [SerializeField, Range(1, 12)] private int harmonicsCount = 6;
    [SerializeField] private WaveformType waveform = WaveformType.Sine;
    [SerializeField] private string input;
    private AudioClip _clip;
    private int _clipCapacity = 0;
    private float[] _managedSamples;

    private void Start()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();


    }

    [Button]
    public void PlayString()
    {
        PlayFromStringAsync(input).Forget();
    }

    private void OnDisable()
    {
        StopAndReleaseClip();
    }

    private void OnDestroy()
    {
        StopAndReleaseClip();
    }
    public async UniTask<AudioClip> PlayFromStringAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(input) || ct.IsCancellationRequested) return null;

        var hash = StableHashService.CreateHashData(input, harmonicsCount);

        int sampleCount = Mathf.CeilToInt(sampleRate * duration);

        EnsureClipCapacity(sampleCount);

        EnsureManagedBuffer(sampleCount);

        await AudioSynthesizer.GenerateSamplesToManagedAsync(
            hash,
            sampleRate,
            duration,
            baseFrequency,
            baseVolume,
            waveform,
            _managedSamples,
            ct
        );

        if (ct.IsCancellationRequested) return null;
        if (_clip == null) return null;


        _clip.SetData(_managedSamples, 0);

        audioSource.clip = _clip;
        audioSource.Play();

        return _clip;
    }

    private void EnsureClipCapacity(int requiredSamples)
    {
        if (_clip != null && requiredSamples <= _clipCapacity) return;

        if (_clip != null)
        {
            if (Application.isPlaying) Destroy(_clip);
            else DestroyImmediate(_clip);
        }

        _clip = AudioClip.Create("StringAudio", requiredSamples, 1, sampleRate, false);
        _clipCapacity = requiredSamples;
    }

    private void EnsureManagedBuffer(int requiredSamples)
    {
        if (_managedSamples == null || _managedSamples.Length < requiredSamples)
        {
            _managedSamples = new float[requiredSamples];
        }
    }

    private void StopAndReleaseClip()
    {
        if (audioSource && audioSource.clip == _clip) audioSource.Stop();
        if (_clip != null)
        {
            if (Application.isPlaying) Destroy(_clip);
            else DestroyImmediate(_clip);
            _clip = null;
            _clipCapacity = 0;
        }
    }
}