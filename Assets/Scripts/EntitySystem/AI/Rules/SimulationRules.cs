using UnityEngine;

[CreateAssetMenu(fileName = "SimulationRules", menuName = "Simulation/Rules")]
public sealed class SimulationRules : ScriptableObject
{
    public readonly struct RulesFrame
    {
        public readonly float EpsilonDecayPerStep;
        public readonly float MinEpsilon;
        public readonly float ExplorationEpsilonScale;
        public readonly float PpoClipEpsilon;
        public readonly float RewardClipSigma;
        public readonly float PathCostPenalty;
        public readonly float PathCostRatioMax;
        public readonly float TerrainBiasWeight;
        public readonly float TerrainCostWeight;
        public readonly float TerrainSlopeWeight;
        public readonly float TerrainWaterWeight;
        public readonly float BasalEnergyDrain;
        public readonly float IdleRegenBasalFraction;
        public readonly float StarvationThreshold;
        public readonly float StarvationDamage;
        public readonly float StarvationEnergyReturn;
        public readonly float FoodRegenPerSecond;
        public readonly float FoodRegenMaxStep;
        public readonly int   FoodRegenBurst;
        public readonly float FitnessSurvivalWeight;

        public readonly float FitnessSurvivalTau;
        public readonly float FitnessRateWindow;
        public readonly float FitnessMinLifetime;
        public readonly float FitnessFoodRateWeight;
        public readonly float FitnessReproduceRateWeight;
        public readonly float FitnessDamageRateWeight;

        public RulesFrame(SimulationRules r)
        {
            EpsilonDecayPerStep     = r.EpsilonDecayPerStep;
            MinEpsilon              = r.MinEpsilon;
            ExplorationEpsilonScale = r.ExplorationEpsilonScale;
            PpoClipEpsilon          = r.PpoClipEpsilon;
            RewardClipSigma         = r.RewardClipSigma;
            PathCostPenalty         = r.PathCostPenalty;
            PathCostRatioMax        = r.PathCostRatioMax;
            TerrainBiasWeight       = r.TerrainBiasWeight;
            TerrainCostWeight       = r.TerrainCostWeight;
            TerrainSlopeWeight      = r.TerrainSlopeWeight;
            TerrainWaterWeight      = r.TerrainWaterWeight;
            BasalEnergyDrain        = r.BasalEnergyDrain;
            IdleRegenBasalFraction  = r.IdleRegenBasalFraction;
            StarvationThreshold     = r.StarvationThreshold;
            StarvationDamage        = r.StarvationDamage;
            StarvationEnergyReturn  = r.StarvationEnergyReturn;
            FoodRegenPerSecond      = r.FoodRegenPerSecond;
            FoodRegenMaxStep        = r.FoodRegenMaxStep;
            FoodRegenBurst          = r.FoodRegenBurst;
            FitnessSurvivalWeight   = r.FitnessSurvivalWeight;

            FitnessSurvivalTau      = r.FitnessSurvivalTau;
            FitnessRateWindow       = r.FitnessRateWindow;
            FitnessMinLifetime      = r.FitnessMinLifetime;
            FitnessFoodRateWeight   = r.FitnessFoodRateWeight;
            FitnessReproduceRateWeight   = r.FitnessReproduceRateWeight;
            FitnessDamageRateWeight = r.FitnessDamageRateWeight;
        }
    }

    private static RulesFrame _frame;
    public static ref readonly RulesFrame Frame => ref _frame;

    public static void CaptureFrame() => _frame = new RulesFrame(Active);

    public static void Bind(SimulationRules rules)
    {
        if (rules != null) _active = rules;
        CaptureFrame();
    }
    [SerializeField] private float idleLowEnergyThreshold = 0.35f;
    [SerializeField] private float idleLongActivityPenaltyStart = 0.85f;
    [SerializeField] private float idleRewardLowEnergy = 0.6f;
    [SerializeField] private float idleRewardOveractive = -0.35f;

    [SerializeField] private float eatHealFood = 30f;
    [SerializeField] private float eatEnergyFood = 20f;
    [SerializeField] private float eatRewardFood = 0.8f;
    [SerializeField] private float eatRewardNoFood = -0.25f;

    [SerializeField] private float restHealRate = 5f;
    [SerializeField] private float restDuration = 3f;
    [SerializeField] private float restDeficitThreshold = 0.15f;
    [SerializeField] private float restRewardNeeded = 0.7f;
    [SerializeField] private float restRewardWasted = -0.2f;

    [SerializeField] private float idleRegenBasalFraction = 0.5f;

    [SerializeField] private float foodRegenPerSecond = 2f;
    [SerializeField] private float foodRegenMaxStep   = 0.25f;
    [SerializeField] private int   foodRegenBurst     = 8;

    [SerializeField] private float starvationDamage = 12f;
    [SerializeField] private float starvationEnergyReturn = 5f;
    [SerializeField] private float starvationThreshold = 5f;

    [SerializeField] private float restEnergyRate = 9f;

    [SerializeField] private float terminalPenalty = -1f;
    [SerializeField] private float interruptionReward = -0.1f;
    [SerializeField] private float failureReward = -0.2f;

    [SerializeField] private float ppoClipEpsilon = 0.2f;
    [SerializeField] private float rewardClipSigma = 5f;
    [SerializeField] private float criticLearningRate = 0.004f;
    [SerializeField] private float criticHuberDelta = 1f;

    [SerializeField] private float explorationEpsilonScale = 1f;
    [SerializeField] private float epsilonDecayPerStep = 5e-5f;
    [SerializeField] private float minEpsilon = 0.01f;
    [SerializeField] private float wanderRewardScale = 0.6f;
    [SerializeField] private float wanderEnergyPenalty = 0.01f;
    [SerializeField] private float wanderRewardMin = -0.2f;
    [SerializeField] private float wanderRewardMax = 0.6f;

    [SerializeField] private float minLearningRate  = 1e-3f;
    [SerializeField] private float lrDecayPerSecond = 5e-4f;
    [SerializeField] private float fitnessSurvivalWeight     = 10f;
    [SerializeField] private float fitnessSurvivalTau        = 60f;
    [SerializeField] private float fitnessRateWindow         = 60f;
    [SerializeField] private float fitnessMinLifetime        = 10f;
    [SerializeField] private float fitnessFoodRateWeight     = 6f;
    [SerializeField] private float fitnessReproduceRateWeight = 12f;
    [SerializeField] private float fitnessDamageRateWeight   = 0.05f;

    [SerializeField] private float terminalFitnessSensitivity = 0.5f;
    [SerializeField] private float terminalPenaltyMinScale    = 0.25f;
    [SerializeField] private float terminalPenaltyMaxScale    = 2f;

    [SerializeField] private int   tournamentSize      = 3;
    [SerializeField] private float generationInterval  = 60f;
    [SerializeField] private float eliteFraction       = 0.1f;
    [SerializeField] private float basalEnergyDrain = 0.5f;

    [SerializeField] private float terrainBiasWeight  = 0.45f;
    [SerializeField] private float terrainCostWeight  = 1f;
    [SerializeField] private float terrainSlopeWeight = 0.8f;
    [SerializeField] private float terrainWaterWeight = 0.6f;
    [SerializeField] private float pathCostPenalty    = 0.15f;
    [SerializeField] private float pathCostRatioMax   = 4f;

    public float TerrainBiasWeight  => terrainBiasWeight;
    public float TerrainCostWeight  => terrainCostWeight;
    public float TerrainSlopeWeight => terrainSlopeWeight;
    public float TerrainWaterWeight => terrainWaterWeight;
    public float PathCostPenalty    => pathCostPenalty;
    public float PathCostRatioMax   => pathCostRatioMax;

    public float BasalEnergyDrain => basalEnergyDrain;

    public float FitnessSurvivalWeight      => fitnessSurvivalWeight;
    public float FitnessSurvivalTau         => fitnessSurvivalTau;
    public float FitnessRateWindow          => fitnessRateWindow;
    public float FitnessMinLifetime         => fitnessMinLifetime;
    public float FitnessFoodRateWeight      => fitnessFoodRateWeight;
    public float FitnessReproduceRateWeight => fitnessReproduceRateWeight;
    public float FitnessDamageRateWeight    => fitnessDamageRateWeight;

    public float TerminalFitnessSensitivity => terminalFitnessSensitivity;
    public float TerminalPenaltyMinScale    => terminalPenaltyMinScale;
    public float TerminalPenaltyMaxScale    => terminalPenaltyMaxScale;

    public int   TournamentSize     => tournamentSize;
    public float GenerationInterval => generationInterval;
    public float EliteFraction      => eliteFraction;

    public float MinLearningRate  => minLearningRate;
    public float LrDecayPerSecond => lrDecayPerSecond;
    public float WanderRewardScale => wanderRewardScale;
    public float WanderEnergyPenalty => wanderEnergyPenalty;
    public float WanderRewardMin => wanderRewardMin;
    public float WanderRewardMax => wanderRewardMax;

    public float IdleLowEnergyThreshold => idleLowEnergyThreshold;
    public float IdleLongActivityPenaltyStart => idleLongActivityPenaltyStart;
    public float IdleRewardLowEnergy => idleRewardLowEnergy;
    public float IdleRewardOveractive => idleRewardOveractive;

    public float EatHealFood => eatHealFood;
    public float EatEnergyFood => eatEnergyFood;
    public float EatRewardFood => eatRewardFood;
    public float EatRewardNoFood => eatRewardNoFood;

    public float RestHealRate => restHealRate;
    public float RestDuration => restDuration;
    public float RestDeficitThreshold => restDeficitThreshold;
    public float RestRewardNeeded => restRewardNeeded;
    public float RestRewardWasted => restRewardWasted;

    public float IdleRegenBasalFraction => idleRegenBasalFraction;

    public float FoodRegenPerSecond => foodRegenPerSecond;
    public float FoodRegenMaxStep   => foodRegenMaxStep;
    public int   FoodRegenBurst     => foodRegenBurst;

    public float StarvationDamage => starvationDamage;
    public float StarvationEnergyReturn => starvationEnergyReturn;
    public float StarvationThreshold => starvationThreshold;

    public float RestEnergyRate => restEnergyRate;

    public float TerminalPenalty => terminalPenalty;
    public float InterruptionReward => interruptionReward;
    public float FailureReward => failureReward;

    public float PpoClipEpsilon => ppoClipEpsilon;
    public float RewardClipSigma => rewardClipSigma;
    public float CriticLearningRate => criticLearningRate;
    public float CriticHuberDelta => criticHuberDelta;

    public float ExplorationEpsilonScale => explorationEpsilonScale;
    public float EpsilonDecayPerStep => epsilonDecayPerStep;
    public float MinEpsilon => minEpsilon;

    private static SimulationRules _active;

    public static SimulationRules Active
    {
        get
        {
            if (_active == null) _active = CreateInstance<SimulationRules>();
            return _active;
        }
    }
}


