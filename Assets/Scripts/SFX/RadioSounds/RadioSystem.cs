using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public class RadioSystem : MonoBehaviour
{
    public static RadioSystem Instance { get; private set; }

    [SerializeField] private RadioLibrary library;
    [SerializeField] private float skipPercentageMin = 0.6f;
    
    private AudioService audioService;
    private Dictionary<int, RadioInstance> activeRadios = new();
    private int nextRadioId = 0;

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
        StopAllRadios();
    }

    public int StartRadio(Vector3 position, Transform parent = null)
    {
        var radioObj = new GameObject($"Radio_{nextRadioId}");
        radioObj.transform.position = position;
        
        if (parent != null)
            radioObj.transform.SetParent(parent);

        return StartRadio(radioObj.transform);
    }

    public int StartRadio(Transform radioTransform)
    {
        if (radioTransform == null)
        {
            Debug.LogError("RadioSystem: radioTransform is null!");
            return -1;
        }

        int radioId = nextRadioId++;
        var instance = new RadioInstance(radioTransform);
        instance.IsPlaying = true;
        
        activeRadios[radioId] = instance;

        RadioLoop(instance).Forget();
        MoodChangeLoop(instance).Forget();

        return radioId;
    }

    public void StopRadio(int radioId)
    {
        if (!activeRadios.TryGetValue(radioId, out var instance))
            return;

        instance.IsPlaying = false;
        instance.Dispose();

        if (instance.CurrentAudio != null)
            audioService?.Stop(instance.CurrentAudio);

        activeRadios.Remove(radioId);

        if (instance.Transform != null && instance.Transform.name.StartsWith("Radio_"))
            Destroy(instance.Transform.gameObject);
    }

    public void StopAllRadios()
    {
        var radioIds = activeRadios.Keys.ToList();
        foreach (var id in radioIds)
            StopRadio(id);
    }

    public void SetRadioMood(int radioId, RadioMood mood)
    {
        if (activeRadios.TryGetValue(radioId, out var instance))
        {
            if (instance.CurrentMood != mood)
            {
                instance.CurrentMood = mood;
                instance.PlayedInCurrentMood.Clear();
            }
        }
    }

    public bool IsRadioPlaying(int radioId)
    {
        return activeRadios.TryGetValue(radioId, out var instance) && instance.IsPlaying;
    }

    public Vector3? GetRadioPosition(int radioId)
    {
        if (activeRadios.TryGetValue(radioId, out var instance) && instance.Transform != null)
            return instance.Transform.position;
        return null;
    }
    public void MoveRadio(int radioId, Vector3 newPosition)
    {
        if (activeRadios.TryGetValue(radioId, out var instance) && instance.Transform != null)
        {
            instance.Transform.position = newPosition;
        }
    }

    private async UniTaskVoid RadioLoop(RadioInstance instance)
    {
        while (instance.IsPlaying && !instance.Cts.Token.IsCancellationRequested)
        {
            try
            {
                var moodClips = library.GetMoodClips(instance.CurrentMood);
                if (moodClips == null || moodClips.Songs.Length == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: instance.Cts.Token);
                    continue;
                }

                var songIndex = GetNextSongIndex(instance, moodClips.Songs.Length);
                var song = moodClips.Songs[songIndex];
                instance.PlayedInCurrentMood.Add(songIndex);

                var config = AudioPlayConfig.Default(song);
                config.Volume = moodClips.Volume;
                config.Position = instance.Transform.position;
                config.Is3D = true;
                
                instance.CurrentAudio = audioService.Play(AudioChannel.SFX, config);

                if (instance.CurrentAudio?.Source != null)
                {
                    instance.CurrentAudio.Source.spatialBlend = 1f;
                    instance.CurrentAudio.Source.rolloffMode = AudioRolloffMode.Logarithmic;
                    instance.CurrentAudio.Source.minDistance = library.MinDistance;
                    instance.CurrentAudio.Source.maxDistance = library.MaxDistance;
                    instance.CurrentAudio.Source.transform.position = instance.Transform.position;
                }

                float songDuration = song.length;
                float playDuration = songDuration * Random.Range(skipPercentageMin, 1f);
                
                await UniTask.Delay((int)(playDuration * 1000), cancellationToken: instance.Cts.Token);
                
                if (instance.CurrentAudio != null)
                    audioService.Stop(instance.CurrentAudio);

                await PlayStatic(instance);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
        }
    }

    private async UniTask PlayStatic(RadioInstance instance)
    {
        var staticClip = library.GetRandomStatic();
        if (staticClip == null) return;

        var config = AudioPlayConfig.Default(staticClip);
        config.Volume = 0.3f;
        config.Position = instance.Transform.position;
        config.Is3D = true;
        
        instance.CurrentAudio = audioService.Play(AudioChannel.SFX, config);
        
        if (instance.CurrentAudio?.Source != null)
        {
            instance.CurrentAudio.Source.spatialBlend = 1f;
            instance.CurrentAudio.Source.minDistance = library.MinDistance;
            instance.CurrentAudio.Source.maxDistance = library.MaxDistance;
        }
        
        float duration = library.GetRandomStaticDuration();
        await UniTask.Delay((int)(duration * 1000), cancellationToken: instance.Cts.Token);
        
        if (instance.CurrentAudio != null)
            audioService.Stop(instance.CurrentAudio);
    }

    private async UniTaskVoid MoodChangeLoop(RadioInstance instance)
    {
        while (instance.IsPlaying && !instance.Cts.Token.IsCancellationRequested)
        {
            await UniTask.Delay((int)(library.MoodChangeCooldown * 1000), cancellationToken: instance.Cts.Token);
            
            var moods = System.Enum.GetValues(typeof(RadioMood)).Cast<RadioMood>().ToArray();
            var newMood = moods[Random.Range(0, moods.Length)];
            
            if (newMood != instance.CurrentMood)
            {
                instance.CurrentMood = newMood;
                instance.PlayedInCurrentMood.Clear();
                Debug.Log($"Radio mood changed to: {instance.CurrentMood}");
            }
        }
    }

    private int GetNextSongIndex(RadioInstance instance, int totalSongs)
    {
        if (instance.PlayedInCurrentMood.Count >= totalSongs)
            instance.PlayedInCurrentMood.Clear();

        int index;
        int attempts = 0;
        do
        {
            index = Random.Range(0, totalSongs);
            attempts++;
        } 
        while (instance.PlayedInCurrentMood.Contains(index) && attempts < 100);

        return index;
    }

    public int GetActiveRadioCount() => activeRadios.Count;

    public int[] GetActiveRadioIds() => activeRadios.Keys.ToArray();
}