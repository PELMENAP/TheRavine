using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

public class AmbientLayerController
{
    private readonly AmbientLayer layer;
    private readonly AudioService audioService;
    private readonly CancellationToken globalToken;
    
    private PooledAudio currentAudio;
    private PooledAudio nextAudio;
    private CancellationTokenSource loopCts;
    private bool isRunning;

    public AmbientLayerController(AmbientLayer layer, AudioService audioService, CancellationToken globalToken)
    {
        this.layer = layer;
        this.audioService = audioService;
        this.globalToken = globalToken;
    }

    public async UniTaskVoid Start()
    {
        isRunning = true;
        loopCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(loopCts.Token, globalToken);

        try
        {
            await PlayLoop(linkedCts.Token);
        }
        catch (System.OperationCanceledException) { }
    }

    private async UniTask PlayLoop(CancellationToken ct)
    {
        if(layer.DelayIsFirst) 
        {
            float delay = layer.GetRandomDelay();
            await UniTask.Delay((int)(delay * 1000), cancellationToken: ct);
        }
        while (isRunning && !ct.IsCancellationRequested)
        {
            var clip = layer.GetRandomClip();
            if (clip == null)
            {
                await UniTask.Delay(1000, cancellationToken: ct);
                continue;
            }

            if (layer.Crossfade && currentAudio != null)
            {
                nextAudio = PlayClip(clip, 0f);
                
                if (nextAudio != null)
                {
                    var fadeOut = currentAudio.FadeOut(layer.FadeSpeed, ct);
                    var fadeIn = nextAudio.FadeVolume(layer.Volume, layer.FadeSpeed, ct);
                    await UniTask.WhenAll(fadeOut, fadeIn);
                }
                
                currentAudio = nextAudio;
            }
            else
            {
                currentAudio = PlayClip(clip, layer.Volume);
            }

            float delay = layer.GetRandomDelay();
            await UniTask.Delay((int)(delay * 1000), cancellationToken: ct);
        }
    }

    private PooledAudio PlayClip(AudioClip clip, float volume)
    {
        var config = AudioPlayConfig.Default(clip);
        config.Volume = volume;
        config.Priority = layer.Priority;
        config.Is3D = false;
        
        return audioService.Play(layer.AudioChannel, config);
    }

    public async UniTask Stop(float fadeTime)
    {
        isRunning = false;
        loopCts?.Cancel();

        if (currentAudio != null)
            await currentAudio.FadeOut(fadeTime);
        
        if (nextAudio != null)
            await nextAudio.FadeOut(fadeTime);

        loopCts?.Dispose();
    }

    public void SetVolume(float volume)
    {
        if (currentAudio?.Source != null)
            currentAudio.Source.volume = volume;
    }
}