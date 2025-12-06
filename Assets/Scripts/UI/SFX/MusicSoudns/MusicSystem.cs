using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

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

public class MusicSystem : MonoBehaviour
{
    public static MusicSystem Instance { get; private set; }

    [SerializeField] private MusicLibrary library;
    
    private AudioService audioService;
    private PooledAudio currentTrack;
    private MusicTrackType? currentTrackType;
    private CancellationTokenSource transitionCts;

    public MusicTrackType? CurrentTrack => currentTrackType;
    public bool IsPlaying => currentTrack != null && currentTrack.Source.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        audioService = AudioService.Instance;
    }

    private void OnDestroy()
    {
        transitionCts?.Cancel();
        transitionCts?.Dispose();
    }

    public async UniTask PlayTrack(MusicTrackType trackType, bool crossfade = true)
    {
        if (library == null || audioService == null) return;

        var track = library.GetTrack(trackType);
        if (track == null) return;

        if (currentTrackType == trackType && IsPlaying) return;

        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        var fadeOutDuration = currentTrack != null ? 
            library.GetTrack(currentTrackType.Value)?.FadeOutDuration ?? 2f : 2f;

        if (crossfade && currentTrack != null)
        {
            var fadeOutTask = currentTrack.FadeOut(fadeOutDuration, transitionCts.Token);
            
            var config = AudioPlayConfig.Default(track.Clip);
            config.Volume = 0f;
            config.Loop = track.Loop;
            
            var newTrack = audioService.Play(AudioChannel.Music, config);
            currentTrack = newTrack;
            currentTrackType = trackType;

            var fadeInTask = newTrack?.FadeVolume(track.Volume, track.FadeInDuration, transitionCts.Token);

            await UniTask.WhenAll(fadeOutTask, fadeInTask ?? UniTask.CompletedTask);
        }
        else
        {
            if (currentTrack != null)
                audioService.Stop(currentTrack);

            var config = AudioPlayConfig.Default(track.Clip);
            config.Volume = track.Volume;
            config.Loop = track.Loop;
            
            currentTrack = audioService.Play(AudioChannel.Music, config);
            currentTrackType = trackType;
        }
    }

    public async UniTask Stop(bool fade = true)
    {
        if (currentTrack == null) return;

        transitionCts?.Cancel();
        transitionCts = new CancellationTokenSource();

        var fadeOutDuration = currentTrackType.HasValue ? 
            library.GetTrack(currentTrackType.Value)?.FadeOutDuration ?? 2f : 2f;

        if (fade)
        {
            await currentTrack.FadeOut(fadeOutDuration, transitionCts.Token);
        }
        else
        {
            audioService?.Stop(currentTrack);
        }

        currentTrack = null;
        currentTrackType = null;
    }

    public void SetVolume(float volume)
    {
        if (currentTrack?.Source != null)
            currentTrack.Source.volume = volume;
    }

    public async UniTask Pause(float fadeDuration = 0.5f)
    {
        if (currentTrack?.Source == null || !currentTrack.Source.isPlaying) return;
        
        await currentTrack.FadeVolume(0f, fadeDuration);
        currentTrack.Source.Pause();
    }

    public async UniTask Resume(float fadeDuration = 0.5f)
    {
        if (currentTrack?.Source == null || currentTrack.Source.isPlaying) return;
        
        var targetVolume = currentTrackType.HasValue ? 
            library.GetTrack(currentTrackType.Value)?.Volume ?? 1f : 1f;
        
        currentTrack.Source.UnPause();
        await currentTrack.FadeVolume(targetVolume, fadeDuration);
    }
}