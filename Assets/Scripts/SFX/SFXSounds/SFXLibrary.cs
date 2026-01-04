using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Audio/SFX Library")]
public class SFXLibrary : ScriptableObject
{
    [SerializeField] private SFXCollection[] collections;
    
    private Dictionary<SFXType, SFXCollection> collectionMap;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        collectionMap = new Dictionary<SFXType, SFXCollection>(collections.Length);
        foreach (var collection in collections)
            collectionMap[collection.Type] = collection;
    }

    public AudioClip GetRandom(SFXType type)
    {
        if (collectionMap == null) BuildLookup();
        
        if (!collectionMap.TryGetValue(type, out var collection) || collection.Clips.Length == 0)
            return null;
        
        return collection.Clips[UnityEngine.Random.Range(0, collection.Clips.Length)];
    }

    public SFXCollection GetCollection(SFXType type)
    {
        if (collectionMap == null) BuildLookup();
        return collectionMap.GetValueOrDefault(type);
    }
}

[Serializable]
public class SFXCollection
{
    [field: SerializeField] public SFXType Type { get; private set; }
    [field: SerializeField] public AudioClip[] Clips { get; private set; }
    [field: SerializeField] public float VolumeMin { get; private set; } = 1f;
    [field: SerializeField] public float VolumeMax { get; private set; } = 1f;
    [field: SerializeField] public float PitchMin { get; private set; } = 0.95f;
    [field: SerializeField] public float PitchMax { get; private set; } = 1.05f;
    [field: SerializeField] public bool Is3D { get; private set; } = true;
    [field: SerializeField] public int Priority { get; private set; } = 0;

    public float GetRandomVolume() => UnityEngine.Random.Range(VolumeMin, VolumeMax);
    public float GetRandomPitch() => UnityEngine.Random.Range(PitchMin, PitchMax);
}

public enum SFXType
{
    Footstep,
    Jump,
    Land,
    Impact,
    Explosion,
    Gunshot,
    Reload,
    PickupItem,
    DropItem,
    Ambient_Wind,
    Ambient_Rain
}