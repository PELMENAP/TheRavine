using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Cysharp.Threading.Tasks;

public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 20;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup uiMixerGroup;

    [Header("Audio Library")]
    [SerializeField] private AudioLibrarySO audioLibrary;

    private readonly Queue<AudioSource> sourcePool = new Queue<AudioSource>();
    private readonly List<AudioSource> activeSources = new List<AudioSource>();
    private float globalVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < poolSize; i++)
        {
            var child = new GameObject($"AudioSource_{i}");
            child.transform.SetParent(transform);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // Default to SFX group
            if (sfxMixerGroup != null)
                src.outputAudioMixerGroup = sfxMixerGroup;
            sourcePool.Enqueue(src);
        }
    }

    // Core SFX playback
    public void PlaySFX(AudioClip clip, Vector2 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || sourcePool.Count == 0)
        {
            if (clip == null)
                Debug.LogWarning("AudioService: Clip is null");
            else
                Debug.LogWarning("AudioService pool exhausted!");
            return;
        }

        var src = sourcePool.Dequeue();
        src.clip = clip;
        src.volume = volume * globalVolume;
        src.pitch = pitch;
        src.transform.position = position;
        src.spatialBlend = 0f; // 2D sound
        src.outputAudioMixerGroup = sfxMixerGroup;
        src.Play();
        activeSources.Add(src);

        ReturnToPoolDelayed(src, clip.length / pitch).Forget();
    }

    // UI-specific playback (2D, global)
    public void PlayUI(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || sourcePool.Count == 0)
        {
            Debug.LogWarning("AudioService: UI clip is null or pool exhausted");
            return;
        }

        var src = sourcePool.Dequeue();
        src.clip = clip;
        src.volume = volume * globalVolume;
        src.pitch = pitch;
        src.transform.position = Vector3.zero;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = uiMixerGroup != null ? uiMixerGroup : sfxMixerGroup;
        src.Play();
        activeSources.Add(src);

        ReturnToPoolDelayed(src, clip.length / pitch).Forget();
    }

    // Library-based playback
    public void PlayFromLibrary(string key, Vector2 position, float volume = 1f, float pitch = 1f)
    {
        var clip = audioLibrary.GetRandomClip(key);
        PlaySFX(clip, position, volume, pitch);
    }
    public void PlayUIFromLibrary(string key, float volume = 1f, float pitch = 1f)
    {
        var clip = audioLibrary.GetRandomClip(key);
        PlayUI(clip, volume, pitch);
    }

    // Example: footsteps as UI sounds for optimization
    public void PlayFootstep(Vector2 position)
    {
        // treat as UI for global, non-spatialized footstep
        PlayUIFromLibrary("footstep");
    }

    public async UniTask PlaySequenceAsync(List<AudioClip> clips, Vector2 position, float volume = 1f)
    {
        if (clips == null || clips.Count == 0) return;

        foreach (var clip in clips)
        {
            if (clip == null) continue;
            PlaySFX(clip, position, volume);
            await UniTask.Delay((int)(clip.length * 1000));
        }
    }

    // Control methods
    public void StopAll()
    {
        foreach (var src in activeSources)
        {
            if (src != null && src.isPlaying)
                src.Stop();
        }
        activeSources.Clear();
    }

    public void StopSound(AudioClip clip)
    {
        foreach (var src in activeSources)
        {
            if (src != null && src.clip == clip && src.isPlaying)
                src.Stop();
        }
    }

    public void SetGlobalVolume(float volume)
    {
        globalVolume = Mathf.Clamp01(volume);
    }

    public int ActiveCount => activeSources.Count;
    public int FreeCount => sourcePool.Count;

    private async UniTaskVoid ReturnToPoolDelayed(AudioSource src, float delay)
    {
        await UniTask.Delay((int)(delay * 1000));
        if (src != null)
        {
            src.clip = null;
            activeSources.Remove(src);
            sourcePool.Enqueue(src);
        }
    }
}


[CreateAssetMenu(menuName = "Audio/Audio Library")]
public class AudioLibrarySO : ScriptableObject
{
    [System.Serializable]
    public class SoundEntry
    {
        public string key;
        public SoundCategory categoryKey;
        public List<AudioClip> clips;
    }

    public List<SoundEntry> entries;

    public List<AudioClip> GetClips(string key)
    {
        return entries.Find(e => e.key == key)?.clips;
    }

    public List<AudioClip> GetClips(SoundCategory key)
    {
        return entries.Find(e => e.categoryKey == key)?.clips;
    }

    public AudioClip GetRandomClip(string key)
    {
        var list = GetClips(key);
        return list != null && list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }

    public AudioClip GetRandomClip(SoundCategory key)
    {
        var list = GetClips(key);
        return list != null && list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }
}

public enum SoundCategory
{
    None,
    FootStep,
}