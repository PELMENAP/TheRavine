using UnityEngine;
using System.Collections.Generic;
using TheRavine.Extensions;

public enum AmbientType
{
    Nature_Day,
    Nature_Night,
    Radio,
    Wind,
    Rain,
    Cave,
    City
}

[System.Serializable]
public class AmbientLayer
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField] public AudioChannel AudioChannel { get; private set; }
    [field: SerializeField] public AudioClip[] Clips { get; private set; }
    [field: SerializeField] public float Volume { get; private set; } = 0.5f;
    [field: SerializeField] public float MinDelay { get; private set; } = 5f;
    [field: SerializeField] public float MaxDelay { get; private set; } = 15f;
    [field: SerializeField] public float FadeSpeed { get; private set; } = 2f;
    [field: SerializeField] public bool Crossfade { get; private set; } = true;
    [field: SerializeField] public bool DelayIsFirst { get; private set; } = true;
    [field: SerializeField] public int Priority { get; private set; } = 0;

    private int lastClipIndex = -1;
    
    public AudioClip GetRandomClip()
    {
        if (Clips == null || Clips.Length == 0) return null;
        if (Clips.Length == 1) return Clips[0];
        
        int index;
        do {
            index = Random.Range(0, Clips.Length);
        } while (index == lastClipIndex && Clips.Length > 1);
        
        lastClipIndex = index;
        return Clips[index];
    }
    
    public float GetRandomDelay() => RavineRandom.RangeFloat(MinDelay, MaxDelay);
}

[System.Serializable]
public class AmbientConfig
{
    [field: SerializeField] public AmbientType Type { get; private set; }
    [field: SerializeField] public AmbientLayer[] Layers { get; private set; }
    [field: SerializeField] public bool AutoTransition { get; private set; } = false;
}

[CreateAssetMenu(fileName = "AmbientLibrary", menuName = "Audio/Ambient Library")]
public class AmbientLibrary : ScriptableObject
{
    [SerializeField] private AmbientConfig[] configs;
    private Dictionary<AmbientType, AmbientConfig> configMap;

    private void OnEnable() => BuildLookup();

    private void BuildLookup()
    {
        configMap = new Dictionary<AmbientType, AmbientConfig>();
        foreach (var config in configs)
            configMap[config.Type] = config;
    }

    public AmbientConfig GetConfig(AmbientType type)
    {
        if (configMap == null) BuildLookup();
        return configMap.GetValueOrDefault(type);
    }
}