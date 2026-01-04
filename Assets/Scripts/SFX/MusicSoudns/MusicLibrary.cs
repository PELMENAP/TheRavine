using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MusicLibrary", menuName = "Audio/Music Library")]
public class MusicLibrary : ScriptableObject
{
    [SerializeField] private MusicTrack[] tracks;
    
    private Dictionary<MusicTrackType, MusicTrack> trackMap;

    private void OnEnable()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        trackMap = new Dictionary<MusicTrackType, MusicTrack>(tracks.Length);
        foreach (var track in tracks)
            trackMap[track.Type] = track;
    }

    public MusicTrack GetTrack(MusicTrackType type)
    {
        if (trackMap == null) BuildLookup();
        return trackMap.GetValueOrDefault(type);
    }
}

[Serializable]
public class MusicTrack
{
    [field: SerializeField] public MusicTrackType Type { get; private set; }
    [field: SerializeField] public AudioClip Clip { get; private set; }
    [field: SerializeField] public float Volume { get; private set; } = 1f;
    [field: SerializeField] public bool Loop { get; private set; } = true;
    [field: SerializeField] public float FadeInDuration { get; private set; } = 2f;
    [field: SerializeField] public float FadeOutDuration { get; private set; } = 2f;
}

public enum MusicTrackType
{
    MainMenu,
    Victory,
    Defeat,
    Credits,
    Cinematic_Intro,
    Cinematic_Ending,
    Shop,
    Tutorial
}