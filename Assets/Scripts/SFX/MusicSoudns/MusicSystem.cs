using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

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