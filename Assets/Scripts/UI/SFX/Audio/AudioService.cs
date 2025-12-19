using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Cysharp.Threading.Tasks;
using System.Threading;
using R3;

public enum AudioChannel { Master, Music, SFX, UI }

public struct AudioPlayConfig
{
    public AudioClip Clip;
    public Vector3 Position;
    public float Volume;
    public float Pitch;
    public int Priority;
    public bool Loop;
    public bool Is3D;
    
    public static AudioPlayConfig Default(AudioClip clip) => new()
    {
        Clip = clip,
        Position = Vector3.zero,
        Volume = 1f,
        Pitch = 1f,
        Priority = 0,
        Loop = false,
        Is3D = false
    };
}

public sealed class PooledAudio : IDisposable
{
    public AudioSource Source;
    public CancellationTokenSource ReturnCts;
    public int Priority;
    public AudioChannel Channel;
    public float PlayStartTime;

    public void Dispose()
    {
        ReturnCts?.Cancel();
        ReturnCts?.Dispose();
        ReturnCts = null;
    }
}

public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup[] channelGroups;

    [Header("Pooling")]
    [SerializeField] private int defaultPoolSize = 32;
    [SerializeField] private ChannelConfig[] channelConfigs;

    private readonly Dictionary<AudioChannel, Stack<PooledAudio>> pools = new();
    private readonly Dictionary<AudioChannel, HashSet<PooledAudio>> activeByChannel = new();
    private readonly Dictionary<AudioChannel, AudioMixerGroup> groupMap = new();
    private readonly Dictionary<AudioChannel, int> channelLimits = new();
    
    private CancellationTokenSource lifetimeCts;
    private readonly Subject<(AudioChannel channel, AudioClip clip)> onSoundPlayed = new();
    
    public Observable<(AudioChannel channel, AudioClip clip)> OnSoundPlayed => onSoundPlayed;

    [Serializable]
    private struct ChannelConfig
    {
        public AudioChannel Channel;
        public AudioMixerGroup Group;
        public int MaxConcurrent;
        public bool Is3D;
        public float MinDistance;
        public float MaxDistance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        
        Instance = this;
        lifetimeCts = new CancellationTokenSource();

        InitializeChannelConfigs();
        InitializePools();
    }

    private void OnDestroy()
    {
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        
        foreach (var channel in activeByChannel.Values)
            foreach (var pooled in channel)
                pooled.Dispose();
        
        onSoundPlayed?.Dispose();
    }

    private void InitializeChannelConfigs()
    {
        foreach (var config in channelConfigs)
        {
            groupMap[config.Channel] = config.Group;
            channelLimits[config.Channel] = config.MaxConcurrent;
        }
        
        foreach (AudioChannel ch in Enum.GetValues(typeof(AudioChannel)))
        {
            if (!activeByChannel.ContainsKey(ch))
                activeByChannel[ch] = new HashSet<PooledAudio>();
            
            if (!channelLimits.ContainsKey(ch))
                channelLimits[ch] = 16;
        }
    }

    private void InitializePools()
    {
        foreach (AudioChannel ch in Enum.GetValues(typeof(AudioChannel)))
        {
            pools[ch] = new Stack<PooledAudio>(defaultPoolSize);
            
            var config = Array.Find(channelConfigs, c => c.Channel == ch);
            int poolCount = Mathf.Min(channelLimits[ch], defaultPoolSize);
            
            for (int i = 0; i < poolCount; i++)
            {
                var pooled = CreateAudioSource(ch, config);
                pools[ch].Push(pooled);
            }
        }
    }

    private PooledAudio CreateAudioSource(AudioChannel channel, ChannelConfig config)
    {
        var go = new GameObject($"Audio_{channel}");
        go.transform.SetParent(transform);
        
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = groupMap.GetValueOrDefault(channel);
        src.spatialBlend = config.Is3D ? 1f : 0f;
        if(config.Is3D)
        {
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = config.MinDistance > 0 ? config.MinDistance : 1f;
            src.maxDistance = config.MaxDistance > 0 ? config.MaxDistance : 50f;
        }
        
        return new PooledAudio { Source = src, Channel = channel };
    }

    public PooledAudio Play(AudioChannel channel, AudioPlayConfig config)
    {
        if (config.Clip == null) return null;

        var activeSet = activeByChannel[channel];
        int limit = channelLimits[channel];
        
        PooledAudio pooled = AcquirePooledAudio(channel, activeSet, limit, config.Priority);
        if (pooled == null) return null;

        ConfigureAndPlay(pooled, config);
        activeSet.Add(pooled);
        
        onSoundPlayed.OnNext((channel, config.Clip));
        
        return pooled;
    }

    private PooledAudio AcquirePooledAudio(AudioChannel channel, HashSet<PooledAudio> activeSet, int limit, int priority)
    {
        if (pools[channel].Count > 0)
            return pools[channel].Pop();

        if (activeSet.Count >= limit)
        {
            var steal = FindLowestPriorityActive(activeSet);
            if (steal != null && steal.Priority < priority)
            {
                StopInternal(steal, false);
                return steal;
            }
            return null;
        }

        var config = Array.Find(channelConfigs, c => c.Channel == channel);
        return CreateAudioSource(channel, config);
    }

    private PooledAudio FindLowestPriorityActive(HashSet<PooledAudio> activeSet)
    {
        PooledAudio lowest = null;
        foreach (var a in activeSet)
        {
            if (lowest == null || a.Priority < lowest.Priority)
                lowest = a;
        }
        return lowest;
    }

    private void ConfigureAndPlay(PooledAudio pooled, AudioPlayConfig config)
    {
        var src = pooled.Source;
        src.clip = config.Clip;
        src.volume = config.Volume;
        src.pitch = config.Pitch;
        src.loop = config.Loop;
        src.transform.position = config.Position;
        src.spatialBlend = config.Is3D ? 1f : 0f;
        
        pooled.Priority = config.Priority;
        pooled.PlayStartTime = Time.time;
        
        src.Play();

        if (!config.Loop)
        {
            pooled.ReturnCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                pooled.ReturnCts.Token, 
                lifetimeCts.Token
            );
            
            float duration = config.Clip.length / Mathf.Abs(config.Pitch);
            ReturnToPoolAfter(pooled, duration, linkedCts.Token).Forget();
        }
    }

    public void Stop(PooledAudio pooled)
    {
        if (pooled == null) return;
        StopInternal(pooled, true);
    }

    private void StopInternal(PooledAudio pooled, bool returnToPool)
    {
        pooled.Dispose();
        
        if (pooled.Source != null && pooled.Source.isPlaying)
            pooled.Source.Stop();
        
        activeByChannel[pooled.Channel].Remove(pooled);
        
        if (returnToPool)
        {
            pooled.Source.clip = null;
            pooled.Source.transform.localPosition = Vector3.zero;
            pools[pooled.Channel].Push(pooled);
        }
    }

    public void StopAll(AudioChannel? channel = null)
    {
        if (channel.HasValue)
        {
            var copy = new List<PooledAudio>(activeByChannel[channel.Value]);
            foreach (var p in copy) Stop(p);
        }
        else
        {
            foreach (var channelSet in activeByChannel.Values)
            {
                var copy = new List<PooledAudio>(channelSet);
                foreach (var p in copy) Stop(p);
            }
        }
    }

    private async UniTaskVoid ReturnToPoolAfter(PooledAudio pooled, float delaySeconds, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: token);
            
            if (token.IsCancellationRequested || pooled?.Source == null) 
                return;
            
            StopInternal(pooled, true);
        }
        catch (OperationCanceledException) { }
    }

    public void SetChannelVolume(AudioChannel channel, float linear0to1)
    {
        string param = $"{channel}Volume";
        float db = LinearToDb(linear0to1);
        
        if (!mixer.SetFloat(param, db))
            Debug.LogWarning($"AudioMixer parameter '{param}' not found");
    }

    public float GetChannelVolume(AudioChannel channel)
    {
        string param = $"{channel}Volume";
        return mixer.GetFloat(param, out float db) ? DbToLinear(db) : 1f;
    }

    public void TransitionToSnapshot(AudioMixerSnapshot snapshot, float time)
    {
        snapshot?.TransitionTo(time);
    }

    public int GetActiveCount(AudioChannel channel) => activeByChannel[channel].Count;

    private static float LinearToDb(float linear) => 
        linear <= 0.0001f ? -80f : 20f * Mathf.Log10(Mathf.Clamp01(linear));
    
    private static float DbToLinear(float db) => 
        db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
}