using System;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public struct TransplantEntry
{
    public float time;
    public int fromColonyId;
    public int toColonyId;
    public int count;
}

public sealed partial class SimulationRules
{
    [SerializeField] private TransplantEntry[] transplantSchedule = Array.Empty<TransplantEntry>();
    [SerializeField] private bool transplantTrainGuests;

    public ReadOnlySpan<TransplantEntry> TransplantSchedule => transplantSchedule;
    public bool TransplantTrainGuests => transplantTrainGuests;

    [SerializeField] private int extraCivilColonies;
    [SerializeField] private float minColonySpacing = 60f;
    [SerializeField] private float extraColonyRadius = 200f;
    [SerializeField] private int extraColonyCandidates = 32;

    [SerializeField] private ReseedMode reseedMode = ReseedMode.CloneBest;
    [SerializeField] private float reseedWeightSigma = 0.05f;
    [SerializeField] private bool fissionEnabled;
    [SerializeField] private int fissionMembers = 24;
    [SerializeField] private float fissionShare = 0.5f;

    [SerializeField] private bool colonySpeechTableEnabled;
    [SerializeField] private bool brainDistanceEnabled;
    [SerializeField] private int brainDistanceMaxColonies = 32;

    [SerializeField] private int motifTopK;
    [SerializeField] private bool canvasExportEnabled;
    [SerializeField] private float canvasDecay = 0.9f;
    [SerializeField] private float canvasTerritorySpan;

    public int MotifTopK => motifTopK;
    public bool CanvasExportEnabled => canvasExportEnabled;
    public float CanvasDecay => canvasDecay;
    public float CanvasTerritorySpan => canvasTerritorySpan;

    public bool ColonySpeechTableEnabled => colonySpeechTableEnabled;
    public bool BrainDistanceEnabled => brainDistanceEnabled;
    public int BrainDistanceMaxColonies => brainDistanceMaxColonies;

    public ReseedMode ReseedMode => reseedMode;
    public float ReseedWeightSigma => reseedWeightSigma;
    public bool FissionEnabled => fissionEnabled;
    public int FissionMembers => fissionMembers;
    public float FissionShare => fissionShare;

    public int ExtraCivilColonies => extraCivilColonies;
    public float MinColonySpacing => minColonySpacing;
    public float ExtraColonyRadius => extraColonyRadius;
    public int ExtraColonyCandidates => extraColonyCandidates;

    private void ValidateCulture()
    {
        extraCivilColonies = math.clamp(extraCivilColonies, 0, ColonyRegistry.MaxColonies - 2);
        if (!(minColonySpacing >= 0f)) minColonySpacing = 0f;
        if (!(extraColonyRadius >= 0f)) extraColonyRadius = 0f;
        extraColonyCandidates = math.clamp(extraColonyCandidates, 1, 1024);
        transplantSchedule ??= Array.Empty<TransplantEntry>();
        for (int i = 0; i < transplantSchedule.Length; i++)
        {
            if (!(transplantSchedule[i].time >= 0f)) transplantSchedule[i].time = 0f;
            if (transplantSchedule[i].count < 0) transplantSchedule[i].count = 0;
        }
        canvasDecay = math.clamp(canvasDecay, 0f, 0.999f);
        if (!(canvasTerritorySpan >= 0f)) canvasTerritorySpan = 0f;
        motifTopK = math.clamp(motifTopK, 0, BehaviourMotifLog.Slots);
        brainDistanceMaxColonies = math.clamp(brainDistanceMaxColonies, 2, ColonyRegistry.MaxColonies);
        if (!(reseedWeightSigma >= 0f)) reseedWeightSigma = 0f;
        if (fissionMembers < 2) fissionMembers = 2;
        fissionShare = math.saturate(fissionShare);
    }
}
