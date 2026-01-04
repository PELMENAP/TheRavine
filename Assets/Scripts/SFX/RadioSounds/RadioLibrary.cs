using UnityEngine;
using System.Threading;
using System.Collections.Generic;

public enum RadioMood
{
    Sad,
    Normal,
    Funny
}

[System.Serializable]
public class RadioMoodClips
{
    [field: SerializeField] public RadioMood Mood { get; private set; }
    [field: SerializeField] public AudioClip[] Songs { get; private set; }
    [field: SerializeField] public float Volume { get; private set; } = 0.5f;
}

[CreateAssetMenu(fileName = "RadioLibrary", menuName = "Audio/Radio Library")]
public class RadioLibrary : ScriptableObject
{
    [SerializeField] private RadioMoodClips[] moodClips;
    [SerializeField] private AudioClip[] stationStatics;
    [SerializeField] private AudioClip victoryClip;
    
    [Header("Timing Settings")]
    [SerializeField] private float minStaticDuration = 3f;
    [SerializeField] private float maxStaticDuration = 5f;
    [SerializeField] private float moodChangeCooldown = 100f;

    [Header("3D Audio Settings")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 30f;
    
    private Dictionary<RadioMood, RadioMoodClips> moodMap;

    private void OnEnable() => BuildLookup();

    private void BuildLookup()
    {
        moodMap = new Dictionary<RadioMood, RadioMoodClips>();
        foreach (var clips in moodClips)
            moodMap[clips.Mood] = clips;
    }

    public RadioMoodClips GetMoodClips(RadioMood mood)
    {
        if (moodMap == null) BuildLookup();
        return moodMap.GetValueOrDefault(mood);
    }

    public AudioClip GetRandomStatic() =>
        stationStatics != null && stationStatics.Length > 0 
            ? stationStatics[Random.Range(0, stationStatics.Length)] 
            : null;

    public float GetRandomStaticDuration() => Random.Range(minStaticDuration, maxStaticDuration);
    public float MoodChangeCooldown => moodChangeCooldown;
    public AudioClip VictoryClip => victoryClip;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
}

public class RadioInstance
{
    public Transform Transform;
    public PooledAudio CurrentAudio;
    public RadioMood CurrentMood;
    public HashSet<int> PlayedInCurrentMood;
    public CancellationTokenSource Cts;
    public bool IsPlaying;

    public RadioInstance(Transform transform)
    {
        Transform = transform;
        CurrentMood = RadioMood.Normal;
        PlayedInCurrentMood = new HashSet<int>();
        Cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        Cts?.Cancel();
        Cts?.Dispose();
    }
}