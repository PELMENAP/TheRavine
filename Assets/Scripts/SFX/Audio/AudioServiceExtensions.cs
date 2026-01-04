using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using R3;
using LitMotion;

public static class AudioServiceExtensions
{
    public static PooledAudio PlayOneShot(this AudioService service, AudioChannel channel, AudioClip clip,
        Vector3? position = null, float volume = 1f, float pitch = 1f)
    {
        var config = AudioPlayConfig.Default(clip);
        config.Position = position ?? Vector3.zero;
        config.Volume = volume;
        config.Pitch = pitch;
        config.Is3D = position.HasValue;
        
        return service.Play(channel, config);
    }

    public static PooledAudio PlayAtPosition(this AudioService service, AudioClip clip, Vector3 position,
        float volume = 1f, float minDistance = 1f, float maxDistance = 50f)
    {
        var config = AudioPlayConfig.Default(clip);
        config.Position = position;
        config.Volume = volume;
        config.Is3D = true;
        
        return service.Play(AudioChannel.SFX, config);
    }

    public static PooledAudio PlayLoop(this AudioService service, AudioChannel channel, AudioClip clip,
        float volume = 1f, float pitch = 1f)
    {
        var config = AudioPlayConfig.Default(clip);
        config.Volume = volume;
        config.Pitch = pitch;
        config.Loop = true;
        
        return service.Play(channel, config);
    }

    public static async UniTask FadeVolume(this PooledAudio pooled, float targetVolume, float duration,
        CancellationToken ct = default)
    {
        if (pooled?.Source == null) return;

        await LMotion.Create(pooled.Source.volume, targetVolume, duration)
            .WithEase(Ease.Linear)
            .Bind(v => pooled.Source.volume = v)
            .AddTo(pooled.Source.gameObject)
            .ToAwaitable(ct);
    }

    public static async UniTask FadeOut(this PooledAudio pooled, float duration, CancellationToken ct = default)
    {
        if (pooled?.Source == null) return;
        
        await pooled.FadeVolume(0f, duration, ct);
        AudioService.Instance?.Stop(pooled);
    }

    public static PooledAudio FadeIn(this AudioService service, AudioChannel channel, AudioClip clip,
        float targetVolume, float duration, CancellationToken ct = default)
    {
        var config = AudioPlayConfig.Default(clip);
        config.Volume = 0f;
        config.Loop = true;
        
        var pooled = service.Play(channel, config);
        if (pooled != null)
            pooled.FadeVolume(targetVolume, duration, ct).Forget();
        
        return pooled;
    }
}