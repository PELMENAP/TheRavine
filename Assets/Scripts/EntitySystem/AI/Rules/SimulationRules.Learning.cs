using UnityEngine;
using Unity.Mathematics;
using TheRavine.EntityControl.Virology;

public sealed partial class SimulationRules
{
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
    [SerializeField] private float minLearningRate = 1e-3f;
    [SerializeField] private float lrDecayPerSecond = 5e-4f;
    [SerializeField] private float fitnessSurvivalWeight = 10f;
    [SerializeField] private float fitnessSurvivalTau = 60f;
    [SerializeField] private float fitnessRateWindow = 60f;
    [SerializeField] private float fitnessMinLifetime = 10f;
    [SerializeField] private float fitnessFoodRateWeight = 6f;
    [SerializeField] private float fitnessReproduceRateWeight = 12f;
    [SerializeField] private float fitnessDamageRateWeight = 0.05f;
    [SerializeField] private float terminalFitnessSensitivity = 0.5f;
    [SerializeField] private float terminalPenaltyMinScale = 0.25f;
    [SerializeField] private float terminalPenaltyMaxScale = 2f;
    [SerializeField] private int tournamentSize = 3;
    [SerializeField] private float generationInterval = 60f;
    [SerializeField] private float fitnessStatsInterval = 1f;
    [SerializeField] private float explorationEntropyTarget = 1.5f;
    [SerializeField] private float minTrainAdvantage = 0.05f;
    [SerializeField] private float coordExplorationEpsilon = 0.05f;
    [SerializeField] private float execExplorationEpsilon = 0.15f;
    [SerializeField] private float fitnessSpreadMedianFraction = 0.25f;
    [SerializeField] private float optimizerBaseLearningRate = 0.01f;
    [SerializeField] private float optimizerWeightDecay = 1e-5f;
    [SerializeField] private float optimizerMaxGradNorm = 3f;
    [SerializeField] private float eliteFraction = 0.1f;
    [SerializeField] private float infeasibleActionHealthPenalty = 0f;
    [SerializeField] private float rewardStdFloor = 0.05f;
    [SerializeField] private float geneSpeedCostExponent = 2f;
    [SerializeField] private float geneEnergyCapacityUpkeep = 0.5f;
    [SerializeField] private float geneDetectionUpkeep = 0.3f;
    [SerializeField] private float geneMinBasalMul = 0.3f;
    [SerializeField] private float initBiasRange = 0.1f;
    [SerializeField] private bool periodicGenotypeOverwrite = false;
    [SerializeField] private int rndSamplesPerSweep = 128;
    [SerializeField] private float rndLearningRate = 0.01f;
    [SerializeField] private float rndNormAlpha = 0.01f;
    [SerializeField] private float epsilonPlateauFitnessThreshold = 5f;
    [SerializeField] private float epsilonPlateauDrop = 0.7f;
    [SerializeField] private float epsilonPlateauMinScale = 0.1f;
    [SerializeField] private float epsilonPlateauThresholdGrowth = 1.2f;
    [SerializeField] private float epsilonPlateauThresholdStep = 0f;
    [SerializeField] private float reservoirSpectralRadius = 0.9f;
    [SerializeField] private float reservoirHz = 0f;
    [SerializeField] private float exploreTemperature = 3f;
    [SerializeField] private float curiosityStorageDamp = 0.5f;
    [SerializeField] private int deathReplayCount = 4;
    [SerializeField] private int deathReplayPasses = 2;
    [SerializeField] private float deathReplayWeight = 1.5f;
    [SerializeField] private float planMinSeconds = 4f;
    [SerializeField] private float planMaxSeconds = 40f;
    [SerializeField] private float planMaskBias = 30f;
    [SerializeField] private float planInfeasibleReward = -0.3f;
    [SerializeField] private float planCompleteReward = 0.2f;
    [SerializeField] private int planMaxFailures = 2;
    [SerializeField] private int planMaxInstantSteps = 6;
    [SerializeField] private float[] deathPenaltyByCause = { 1f, 1.5f, 0.6f, 0.6f, 0.3f };
    [SerializeField] private Vector3[] planBoidMul =
    {
        new(1.6f, 0.6f, 0.3f),
        new(1.6f, 0.6f, 0.3f),
        new(1.3f, 0.8f, 0.5f),
        new(1f, 1f, 0.8f),
        new(1f, 1f, 1f),
        new(0.8f, 1.4f, 1.6f),
        new(1f, 1f, 1f),
        new(0.8f, 1.5f, 1.8f),
        new(1f, 1f, 1f),
    };
    [SerializeField] private float fitnessPersonalWeight = 1f;
    [SerializeField] private float fitnessColonyWeight = 5f;
    [SerializeField] private float planRewardEmaAlpha = 0.1f;
    [SerializeField] private float utilityPriorWeight = 1f;
    [SerializeField] private float tempDopamine = 0.5f;
    [SerializeField] private float tempCortisol = 0.5f;
    [SerializeField] private float tempSurprise = 0.5f;
    [SerializeField] private float tempMin = 0.5f;
    [SerializeField] private float tempMax = 2f;
    [SerializeField] private float cortisolPanicShift = 0.3f;
    [SerializeField] private float surpriseAlpha = 0.1f;
    [SerializeField] private float restRewardPerSecond = 0.02f;
    [SerializeField] private float planMinTravel = 15f;
    [SerializeField] private float planMaxTravel = 60f;
    [SerializeField] private float planArriveRadius = 3f;
    [SerializeField] private int planMaxHops = 4;
    [SerializeField] private float[] planMinSecondsByPlan = { 4f, 4f, 6f, 3f, 4f, 3f, 4f, 10f, 12f };
    [SerializeField] private float[] planMaxSecondsByPlan = { 30f, 60f, 45f, 20f, 30f, 15f, 20f, 60f, 90f };
    [SerializeField] private float[] utilityWeights =
    {
        2.0f, -1.5f, 0.0f, -1.0f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f,
        0.3f, 0.5f, -1.0f, -1.0f, -0.5f, 0.3f, 1.0f, -0.5f, 0.0f, -0.3f, -0.3f, 1.2f,
        0.3f, 0.0f, -0.3f, -0.5f, -0.5f, 0.0f, 0.0f, 0.3f, 0.8f, -0.5f, -0.3f, 0.0f,
        -0.5f, 0.0f, 0.0f, 0.8f, -0.5f, 0.0f, -0.3f, 1.0f, -0.3f, -0.5f, -0.5f, 0.0f,
        -1.5f, 0.5f, 0.2f, -0.5f, 1.0f, 0.0f, 0.0f, 0.0f, -0.3f, 0.3f, 1.5f, 0.0f,
        0.0f, 0.0f, 0.0f, 2.0f, 0.0f, 0.0f, -0.3f, -0.5f, 0.3f, 0.0f, 0.5f, 0.0f,
        -1.5f, 0.5f, 0.3f, -1.0f, -0.3f, 0.5f, 0.0f, 0.0f, 0.0f, 1.0f, -0.5f, -0.5f,
        0.0f, 0.0f, -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f, -0.5f, 0.0f,
        1.5f, -1.0f, -1.0f, -1.0f, -0.5f, 0.0f, 0.3f, -0.3f, 1.0f, -0.5f, 0.0f, 0.5f,
    };
    [SerializeField] private int[] planCasteDomain = { -1, 0, 2, 1, -1, -1, 3, 2, 2 };
    [SerializeField] private float driveEnergyWeight = 1f;
    [SerializeField] private float driveHealthWeight = 1f;
    [SerializeField] private float driveRewardWeight = 0.1f;
    [SerializeField] private float driveShapingWeight = 1f;
    [SerializeField] private float eventReproduceReward = 1f;
    [SerializeField] private float infeasibleActionReward = -0.15f;
    [SerializeField] private float goalFoodWeight = 0.3f;
    [SerializeField] private float goalEnergyWeight = 1f;
    [SerializeField] private float goalRestWeight = 0.05f;
    [SerializeField] private float coordCuriosityWeight = 0.15f;
    [SerializeField] private float execGammaPerSecond = 0.9f;
    [SerializeField] private float coordGammaPerSecond = 0.97f;
    [SerializeField] private EpsilonSchedule epsilonScheduleMode = EpsilonSchedule.Exponential;

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
    public float MinLearningRate => minLearningRate;
    public float LrDecayPerSecond => lrDecayPerSecond;
    public float FitnessSurvivalWeight => fitnessSurvivalWeight;
    public float FitnessSurvivalTau => fitnessSurvivalTau;
    public float FitnessRateWindow => fitnessRateWindow;
    public float FitnessMinLifetime => fitnessMinLifetime;
    public float FitnessFoodRateWeight => fitnessFoodRateWeight;
    public float FitnessReproduceRateWeight => fitnessReproduceRateWeight;
    public float FitnessDamageRateWeight => fitnessDamageRateWeight;
    public float TerminalFitnessSensitivity => terminalFitnessSensitivity;
    public float TerminalPenaltyMinScale => terminalPenaltyMinScale;
    public float TerminalPenaltyMaxScale => terminalPenaltyMaxScale;
    public int TournamentSize => tournamentSize;
    public float GenerationInterval => generationInterval;
    public float FitnessStatsInterval => fitnessStatsInterval;
    public float ExplorationEntropyTarget => explorationEntropyTarget;
    public float MinTrainAdvantage => minTrainAdvantage;
    public float CoordExplorationEpsilon => coordExplorationEpsilon;
    public float ExecExplorationEpsilon => execExplorationEpsilon;
    public float FitnessSpreadMedianFraction => fitnessSpreadMedianFraction;
    public float OptimizerBaseLearningRate => optimizerBaseLearningRate;
    public float OptimizerWeightDecay => optimizerWeightDecay;
    public float OptimizerMaxGradNorm => optimizerMaxGradNorm;
    public float EliteFraction => eliteFraction;
    public float InfeasibleActionHealthPenalty => infeasibleActionHealthPenalty;
    public float RewardStdFloor => rewardStdFloor;
    public float GeneSpeedCostExponent => geneSpeedCostExponent;
    public float GeneEnergyCapacityUpkeep => geneEnergyCapacityUpkeep;
    public float GeneDetectionUpkeep => geneDetectionUpkeep;
    public float GeneMinBasalMul => geneMinBasalMul;
    public float InitBiasRange => initBiasRange;
    public bool PeriodicGenotypeOverwrite => periodicGenotypeOverwrite;
    public int RndSamplesPerSweep => rndSamplesPerSweep;
    public float RndLearningRate => rndLearningRate;
    public float RndNormAlpha => rndNormAlpha;
    public float EpsilonPlateauFitnessThreshold => epsilonPlateauFitnessThreshold;
    public float EpsilonPlateauDrop => epsilonPlateauDrop;
    public float EpsilonPlateauMinScale => epsilonPlateauMinScale;
    public float EpsilonPlateauThresholdGrowth => epsilonPlateauThresholdGrowth;
    public float EpsilonPlateauThresholdStep => epsilonPlateauThresholdStep;
    public float ReservoirSpectralRadius => reservoirSpectralRadius;
    public float ReservoirHz => reservoirHz;
    public float ExploreTemperature => exploreTemperature;
    public float CuriosityStorageDamp => curiosityStorageDamp;
    public int DeathReplayCount => deathReplayCount;
    public int DeathReplayPasses => deathReplayPasses;
    public float DeathReplayWeight => deathReplayWeight;
    public float PlanMinSeconds => planMinSeconds;
    public float PlanMaxSeconds => planMaxSeconds;
    public float PlanMaskBias => planMaskBias;
    public float PlanInfeasibleReward => planInfeasibleReward;
    public float PlanCompleteReward => planCompleteReward;
    public int PlanMaxFailures => planMaxFailures;
    public int PlanMaxInstantSteps => planMaxInstantSteps;
    public float FitnessPersonalWeight => fitnessPersonalWeight;
    public float FitnessColonyWeight => fitnessColonyWeight;
    public float PlanRewardEmaAlpha => planRewardEmaAlpha;
    public float UtilityPriorWeight => utilityPriorWeight;
    public float TempDopamine => tempDopamine;
    public float TempCortisol => tempCortisol;
    public float TempSurprise => tempSurprise;
    public float TempMin => tempMin;
    public float TempMax => tempMax;
    public float CortisolPanicShift => cortisolPanicShift;
    public float SurpriseAlpha => surpriseAlpha;
    public float RestRewardPerSecond => restRewardPerSecond;
    public float PlanMinTravel => planMinTravel;
    public float PlanMaxTravel => planMaxTravel;
    public float PlanArriveRadius => planArriveRadius;
    public int PlanMaxHops => planMaxHops;
    public float DriveEnergyWeight => driveEnergyWeight;
    public float DriveHealthWeight => driveHealthWeight;
    public float DriveRewardWeight => driveRewardWeight;
    public float DriveShapingWeight => driveShapingWeight;
    public float EventReproduceReward => eventReproduceReward;
    public float InfeasibleActionReward => infeasibleActionReward;
    public float GoalFoodWeight => goalFoodWeight;
    public float GoalEnergyWeight => goalEnergyWeight;
    public float GoalRestWeight => goalRestWeight;
    public float CoordCuriosityWeight => coordCuriosityWeight;
    public float ExecGammaPerSecond => execGammaPerSecond;
    public float CoordGammaPerSecond => coordGammaPerSecond;
}
