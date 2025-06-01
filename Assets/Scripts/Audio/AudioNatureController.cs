using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

using TheRavine.Base;
using TheRavine.Services;
using Random = TheRavine.Extensions.RavineRandom;
public class AudioNatureController : MonoBehaviour, ISetAble
{
    [SerializeField] private AudioSource[] audioSources;
    [SerializeField] private AudioClip[] dayClips, nightClips;
    [SerializeField] private AudioSource ostSource;
    [SerializeField] private AudioClip[] ostClips;
    [SerializeField] private float maxOstVolume = 0.5f, maxNatureVolume = 0.5f;
    [SerializeField] private float fadeSpeed = 0.002f;

    private CancellationTokenSource _cts;
    private DayCycle dayCycle;
    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        dayCycle = locator.GetService<DayCycle>();

        _cts = new CancellationTokenSource();
        AudioLoop(_cts.Token).Forget();
        OstLoop(_cts.Token).Forget();
        callback?.Invoke();
    }

    private async UniTaskVoid AudioLoop(CancellationToken token)
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            int currentSourceIndex = audioSources[0].isPlaying ? 1 : 0;
            int nextSourceIndex = currentSourceIndex == 0 ? 1 : 0;

            audioSources[nextSourceIndex].clip = dayCycle.IsDay 
                ? dayClips[Random.RangeInt(0, dayClips.Length)] 
                : nightClips[Random.RangeInt(0, nightClips.Length)];
            audioSources[nextSourceIndex].Play();

            await ChangeAudioAsync(currentSourceIndex, nextSourceIndex, token);

            float clipLength = audioSources[nextSourceIndex].clip.length;
            await UniTask.Delay((int)(clipLength - Random.RangeFloat(clipLength / 8, clipLength / 2)), cancellationToken: token);
        }
    }

    private async UniTask ChangeAudioAsync(int from, int to, CancellationToken token)
    {
        while (audioSources[to].volume < maxNatureVolume && !token.IsCancellationRequested)
        {
            audioSources[to].volume += fadeSpeed;
            audioSources[from].volume -= fadeSpeed * 2;
            await UniTask.Yield();
        }
        audioSources[from].Stop();
    }

    private async UniTaskVoid OstLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(Random.RangeInt(30, 100) * 1000, cancellationToken: token);
            await PlayOstAsync(Random.RangeInt(0, ostClips.Length), token);
        }
    }

    private async UniTask PlayOstAsync(int clipIndex, CancellationToken token)
    {
        ostSource.clip = ostClips[clipIndex];
        ostSource.Play();

        while (ostSource.volume < maxOstVolume && !token.IsCancellationRequested)
        {
            ostSource.volume += fadeSpeed;
            await UniTask.Yield();
        }

        float clipLength = ostSource.clip.length;
        await UniTask.Delay((int)(clipLength - Random.RangeFloat(clipLength / 8, clipLength / 2)), cancellationToken: token);

        while (ostSource.volume > 0 && !token.IsCancellationRequested)
        {
            ostSource.volume -= fadeSpeed;
            await UniTask.Yield();
        }
    }

    public void BreakUp(ISetAble.Callback callback)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        callback?.Invoke();
    }
}