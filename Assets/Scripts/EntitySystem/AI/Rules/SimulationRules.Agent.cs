using UnityEngine;
using Unity.Mathematics;
using TheRavine.EntityControl.Virology;

public sealed partial class SimulationRules
{
    [SerializeField] private float restHealRate = 5f;
    [SerializeField] private float startupDelaySeconds = 3f;
    [SerializeField] private float tickLoopWarmupSeconds = 3f;
    [SerializeField] private float agentTickInterval = 1f;
    [SerializeField] private float cycleFallbackDt = 1f / 60f;
    [SerializeField] private float childSpawnOffset = 2f;
    [SerializeField] private float childSpawnLift = 5f;
    [SerializeField] private float maxCycleDt = 3f;
    [SerializeField] private float modifierDecayTau = 85f;
    [SerializeField] private float commandWatchdogGrace = 0.1f;
    [SerializeField] private float restHealEnergyCost = 0.5f;
    [SerializeField] private float speechEnergyCost = 5f;
    [SerializeField] private float dangerLowFraction = 0.5f;
    [SerializeField] private float dangerMidFraction = 0.25f;
    [SerializeField] private float dangerCritFraction = 0.1f;
    [SerializeField] private float rememberPointMinSpacing = 10f;
    [SerializeField] private float actionTraceHorizon = 120f;
    [SerializeField] private float stressTau = 90f;
    [SerializeField] private int huntMaxStrikes = 3;
    [SerializeField] private int restMaxSteps = 3;
    [SerializeField] private float panicHpRelease = 0.15f;
    [SerializeField] private float panicDangerRelease = 0.6f;
    [SerializeField] private float threatMinDanger = 0.1f;
    [SerializeField] private float statsEmaAlpha = 0.1f;
    [SerializeField] private float fearInvertStrength = 1.5f;
    [SerializeField] private float summitHpFraction = 0.25f;
    [SerializeField] private float summitThreshold = 0.3f;
    [SerializeField] private int homeCompulsionCrowd = 3;
    [SerializeField] private float homeCompulsionRestBias = 1.5f;
    [SerializeField] private float isolateStrength = 1f;
    [SerializeField] private float hungerReinterruptSeconds = 8f;
    [SerializeField] private float restMinEnergyReserve = 5f;
    [SerializeField] private float baseMaxAge = 1200f;
    [SerializeField] private float baseMaxHealth = 200f;
    [SerializeField] private float senescenceOnset = 0.7f;
    [SerializeField] private float senescenceUpkeep = 1.5f;
    [SerializeField] private float groomAmount = 0.2f;
    [SerializeField] private float groomMinLoad = 0.2f;
    [SerializeField] private float hormoneTau = 30f;
    [SerializeField] private float oxytocinPanicShift = 0.3f;
    [SerializeField] private float oxytocinNorm = 5f;
    [SerializeField] private float restSocialBonus = 0.5f;
    [SerializeField] private int restSocialMax = 4;
    [SerializeField] private Vector4[] speechSignals =
    {
        new(1f, -1f, 1f, -1f),
        new(1f, 1f, -1f, -1f),
        new(-1f, 1f, 1f, -1f),
        new(-1f, -1f, -1f, 1f),
    };
    [SerializeField] private float synergyWindow = 6f;
    [SerializeField] private float warnWindow = 4f;
    [SerializeField] private float warnDangerThreshold = 0.5f;
    [SerializeField] private float speechRadius = 12f;
    [SerializeField] private float skipWeakHpFraction = 0.3f;
    [SerializeField] private int skipCrowdCount = 3;
    [SerializeField] private float crowdRadius = 4f;
    [SerializeField] private float biteTransmissionChance = 0.2f;
    [SerializeField] private int captureLength = 6;

    public float RestHealRate => restHealRate;
    public float StartupDelaySeconds => startupDelaySeconds;
    public float TickLoopWarmupSeconds => tickLoopWarmupSeconds;
    public float AgentTickInterval => agentTickInterval;
    public float CycleFallbackDt => cycleFallbackDt;
    public float ChildSpawnOffset => childSpawnOffset;
    public float ChildSpawnLift => childSpawnLift;
    public float MaxCycleDt => maxCycleDt;
    public float ModifierDecayTau => modifierDecayTau;
    public float CommandWatchdogGrace => commandWatchdogGrace;
    public float RestHealEnergyCost => restHealEnergyCost;
    public float SpeechEnergyCost => speechEnergyCost;
    public float DangerLowFraction => dangerLowFraction;
    public float DangerMidFraction => dangerMidFraction;
    public float DangerCritFraction => dangerCritFraction;
    public float RememberPointMinSpacing => rememberPointMinSpacing;
    public float ActionTraceHorizon => actionTraceHorizon;
    public float StressTau => stressTau;
    public int HuntMaxStrikes => huntMaxStrikes;
    public int RestMaxSteps => restMaxSteps;
    public float PanicHpRelease => panicHpRelease;
    public float PanicDangerRelease => panicDangerRelease;
    public float ThreatMinDanger => threatMinDanger;
    public float StatsEmaAlpha => statsEmaAlpha;
    public float FearInvertStrength => fearInvertStrength;
    public float SummitHpFraction => summitHpFraction;
    public float SummitThreshold => summitThreshold;
    public int HomeCompulsionCrowd => homeCompulsionCrowd;
    public float HomeCompulsionRestBias => homeCompulsionRestBias;
    public float IsolateStrength => isolateStrength;
    public float HungerReinterruptSeconds => hungerReinterruptSeconds;
    public float RestMinEnergyReserve => restMinEnergyReserve;
    public float BaseMaxAge => baseMaxAge;
    public float BaseMaxHealth => baseMaxHealth;
    public float SenescenceOnset => senescenceOnset;
    public float SenescenceUpkeep => senescenceUpkeep;
    public float GroomAmount => groomAmount;
    public float GroomMinLoad => groomMinLoad;
    public float HormoneTau => hormoneTau;
    public float OxytocinPanicShift => oxytocinPanicShift;
    public float OxytocinNorm => oxytocinNorm;
    public float RestSocialBonus => restSocialBonus;
    public int RestSocialMax => restSocialMax;
    public float SynergyWindow => synergyWindow;
    public float WarnWindow => warnWindow;
    public float WarnDangerThreshold => warnDangerThreshold;
    public float SpeechRadius => speechRadius;
    public float SkipWeakHpFraction => skipWeakHpFraction;
    public int SkipCrowdCount => skipCrowdCount;
    public float CrowdRadius => crowdRadius;
    public float BiteTransmissionChance => biteTransmissionChance;
    public int CaptureLength => captureLength;
}
