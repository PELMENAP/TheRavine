using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UISoundLibrary", menuName = "Audio/UI Sound Library")]
public class UISoundLibrary : ScriptableObject
{
    [SerializeField] private UISoundData[] sounds;
    
    private Dictionary<UISoundType, UISoundData> soundMap;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        soundMap = new Dictionary<UISoundType, UISoundData>(sounds.Length);
        foreach (var data in sounds)
            soundMap[data.Type] = data;
    }

    public UISoundData GetData(UISoundType type)
    {
        if (soundMap == null) BuildLookup();
        return soundMap.GetValueOrDefault(type);
    }

    public AudioClip GetRandomClip(UISoundType type)
    {
        var data = GetData(type);
        if (data == null || data.Clips.Length == 0) return null;
        return data.Clips[UnityEngine.Random.Range(0, data.Clips.Length)];
    }
}

[Serializable]
public class UISoundData
{
    [field: SerializeField] public UISoundType Type { get; private set; }
    [field: SerializeField] public AudioClip[] Clips { get; private set; }
    [field: SerializeField] public float Volume { get; private set; } = 1f;
    [field: SerializeField] public float PitchMin { get; private set; } = 1f;
    [field: SerializeField] public float PitchMax { get; private set; } = 1f;
    [field: SerializeField] public int Priority { get; private set; } = 0;

    public float GetRandomPitch() => 
        Mathf.Approximately(PitchMin, PitchMax) ? PitchMin : UnityEngine.Random.Range(PitchMin, PitchMax);
}

public enum UISoundType
{
    Click,
    Hover,
    Deny,
    Confirm,
    Open,
    Close,
    Success,
    Error,
    Notification
}