using UnityEngine;
using Unity.Mathematics;
using TheRavine.EntityControl.Virology;

public sealed partial class SimulationRules
{
    [SerializeField, FrameField] private float terminalPenalty = -1f;
    [SerializeField, FrameField] private float interruptionReward = -0.1f;
    [SerializeField, FrameField] private float failureReward = -0.2f;
    [SerializeField, FrameField] private float ppoClipEpsilon = 0.2f;
    [SerializeField, FrameField] private bool gradientConflictEnabled;
    [SerializeField] private int gradientConflictInterval = 8;
    [SerializeField, FrameField] private float rewardClipSigma = 5f;
    [SerializeField, FrameField] private float criticLearningRate = 0.004f;
    [SerializeField, FrameField] private float criticHuberDelta = 1f;
    [SerializeField, FrameField] private float explorationEpsilonScale = 1f;
    [SerializeField, FrameField] private float epsilonDecayPerStep = 5e-5f;
    [SerializeField, FrameField] private float minEpsilon = 0.01f;
    [SerializeField, FrameField] private float minLearningRate = 1e-3f;
    [SerializeField, FrameField] private float lrDecayPerSecond = 5e-4f;
    [SerializeField, FrameField] private float fitnessSurvivalWeight = 10f;
    [SerializeField, FrameField] private float fitnessSurvivalTau = 60f;
    [SerializeField, FrameField] private float fitnessRateWindow = 60f;
    [SerializeField, FrameField] private float fitnessMinLifetime = 10f;
    [SerializeField, FrameField] private float fitnessFoodRateWeight = 6f;
    [SerializeField, FrameField] private float fitnessReproduceRateWeight = 12f;
    [SerializeField, FrameField] private float fitnessDamageRateWeight = 0.05f;
    [SerializeField, FrameField] private float terminalFitnessSensitivity = 0.5f;
    [SerializeField, FrameField] private float terminalPenaltyMinScale = 0.25f;
    [SerializeField, FrameField] private float terminalPenaltyMaxScale = 2f;
    [SerializeField, FrameField] private int tournamentSize = 3;
    [SerializeField, FrameField] private float generationInterval = 60f;
    [SerializeField] private float fitnessStatsInterval = 1f;
    [SerializeField, FrameField] private float explorationEntropyTarget = 1.5f;
    [SerializeField, FrameField] private float minTrainAdvantage = 0.05f;
    [SerializeField, FrameField] private float coordExplorationEpsilon = 0.05f;
    [SerializeField, FrameField] private float execExplorationEpsilon = 0.15f;
    [SerializeField] private float fitnessSpreadMedianFraction = 0.25f;
    [SerializeField] private float outlierSigma = 3f;
    [SerializeField] private float optimizerBaseLearningRate = 0.01f;
    [SerializeField] private float optimizerWeightDecay = 1e-5f;
    [SerializeField] private float optimizerMaxGradNorm = 3f;
    [SerializeField, FrameField] private float eliteFraction = 0.1f;
    [SerializeField, FrameField] private float infeasibleActionHealthPenalty = 0f;
    [SerializeField, FrameField] private float rewardStdFloor = 0.05f;
    [SerializeField, FrameField] private float geneSpeedCostExponent = 2f;
    [SerializeField, FrameField] private float geneEnergyCapacityUpkeep = 0.5f;
    [SerializeField, FrameField] private float geneDetectionUpkeep = 0.3f;
    [SerializeField, FrameField] private float geneMinBasalMul = 0.3f;
    [SerializeField, FrameField] private float initBiasRange = 0.1f;
    [SerializeField, FrameField] private bool periodicGenotypeOverwrite = false;
    [SerializeField, FrameField] private int rndSamplesPerSweep = 128;
    [SerializeField, FrameField] private float rndLearningRate = 0.01f;
    [SerializeField, FrameField] private float rndNormAlpha = 0.01f;
    [SerializeField, FrameField] private float epsilonPlateauFitnessThreshold = 5f;
    [SerializeField, FrameField] private float epsilonPlateauDrop = 0.7f;
    [SerializeField, FrameField] private float epsilonPlateauMinScale = 0.1f;
    [SerializeField, FrameField] private float epsilonPlateauThresholdGrowth = 1.2f;
    [SerializeField, FrameField] private float epsilonPlateauThresholdStep = 0f;
    [SerializeField, FrameField] private float reservoirSpectralRadius = 0.9f;
    [SerializeField] private float ltcTauMin = DelayedPerceptron.TauEpsilon;
    [SerializeField] private float ltcTauMax = float.MaxValue;
    [SerializeField] private float noveltyFastRate = 0.2f;
    [SerializeField] private float noveltySlowRate = 0.02f;
    [SerializeField, FrameField] private float reservoirHz = 0f;
    [SerializeField, FrameField] private float exploreTemperature = 3f;
    [SerializeField, FrameField] private float curiosityStorageDamp = 0.5f;
    [SerializeField, FrameField] private int deathReplayCount = 4;
    [SerializeField, FrameField] private int deathReplayPasses = 2;
    [SerializeField, FrameField] private float deathReplayWeight = 1.5f;
    [SerializeField, FrameField] private float planMinSeconds = 4f;
    [SerializeField, FrameField] private float planMaxSeconds = 40f;
    [SerializeField, FrameField] private float planMaskBias = 30f;
    [SerializeField, FrameField] private float planInfeasibleReward = -0.3f;
    [SerializeField, FrameField] private float planCompleteReward = 0.2f;
    [SerializeField, FrameField] private int planMaxFailures = 2;
    [SerializeField, FrameField] private int planMaxInstantSteps = 6;
    [SerializeField] private float[] deathPenaltyByCause = { 1f, 1.5f, 0.6f, 0.6f, 0.3f, 1f };
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
    [SerializeField, FrameField] private float fitnessPersonalWeight = 1f;
    [SerializeField, FrameField] private float fitnessColonyWeight = 5f;
    [SerializeField, FrameField] private float planRewardEmaAlpha = 0.1f;
    [SerializeField, FrameField] private float utilityPriorWeight = 1f;
    [SerializeField, FrameField] private float tempDopamine = 0.5f;
    [SerializeField, FrameField] private float tempCortisol = 0.5f;
    [SerializeField, FrameField] private float tempSurprise = 0.5f;
    [SerializeField, FrameField] private float tempMin = 0.5f;
    [SerializeField, FrameField] private float tempMax = 2f;
    [SerializeField, FrameField] private float cortisolPanicShift = 0.3f;
    [SerializeField, FrameField] private float surpriseAlpha = 0.1f;
    [SerializeField, FrameField] private float restRewardPerSecond = 0.02f;
    [SerializeField, FrameField] private float planMinTravel = 15f;
    [SerializeField, FrameField] private float planMaxTravel = 120f;
    [SerializeField, FrameField] private float planArriveRadius = 3f;
    [SerializeField, FrameField] private int planMaxHops = 4;
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
    [SerializeField, FrameField] private float driveEnergyWeight = 1f;
    [SerializeField, FrameField] private float driveHealthWeight = 1f;
    [SerializeField, FrameField] private float driveRewardWeight = 0.1f;
    [SerializeField, FrameField] private float driveShapingWeight = 1f;
    [SerializeField, FrameField] private float eventReproduceReward = 1f;
    [SerializeField] private float damageEventReward = 0.2f;
    [SerializeField] private float approachRewardWeight = 0f;
    [SerializeField] private float shareFoodReward = 0f;
    [SerializeField] private float rememberFoodReward = 0f;
    [SerializeField, FrameField] private float infeasibleActionReward = -0.15f;
    [SerializeField, FrameField] private float goalFoodWeight = 0.3f;
    [SerializeField, FrameField] private float goalEnergyWeight = 1f;
    [SerializeField, FrameField] private float goalRestWeight = 0.05f;
    [SerializeField] private float digestRewardWeight = 0f;
    [SerializeField, FrameField] private float coordCuriosityWeight = 0.15f;
    [SerializeField, FrameField] private float curiosityEnergyGain = 1f;
    [SerializeField, FrameField] private float curiosityEnergyFloor = 0.6f;
    [SerializeField, FrameField] private float curiosityRarityWeight = 0.1f;
    [SerializeField, Range(0f, 1f)] private float rareActionChance;
    [SerializeField] private bool wideSocialExecutor;
    public bool WideSocialExecutor => wideSocialExecutor;
    [SerializeField, FrameField] private float execGammaPerSecond = 0.9f;
    [SerializeField, FrameField] private float coordGammaPerSecond = 0.97f;
    [SerializeField] private EpsilonSchedule epsilonScheduleMode = EpsilonSchedule.Exponential;

    public float TerminalPenalty => terminalPenalty;
    public float InterruptionReward => interruptionReward;
    public float FailureReward => failureReward;
    public float PpoClipEpsilon => ppoClipEpsilon;
    public bool GradientConflictEnabled => gradientConflictEnabled;
    public int GradientConflictInterval => gradientConflictInterval;
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
    public float OutlierSigma => outlierSigma;
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
    public float LtcTauMin => ltcTauMin;
    public float LtcTauMax => ltcTauMax;
    public float NoveltyFastRate => noveltyFastRate;
    public float NoveltySlowRate => noveltySlowRate;
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
    public float DamageEventReward => damageEventReward;
    public float ApproachRewardWeight => approachRewardWeight;
    public float ShareFoodReward => shareFoodReward;
    public float RememberFoodReward => rememberFoodReward;
    public float InfeasibleActionReward => infeasibleActionReward;
    public float GoalFoodWeight => goalFoodWeight;
    public float GoalEnergyWeight => goalEnergyWeight;
    public float GoalRestWeight => goalRestWeight;
    public float DigestRewardWeight => digestRewardWeight;
    public float CoordCuriosityWeight => coordCuriosityWeight;
    public float CuriosityEnergyGain => curiosityEnergyGain;
    public float CuriosityEnergyFloor => curiosityEnergyFloor;
    public float CuriosityRarityWeight => curiosityRarityWeight;
    public float ExecGammaPerSecond => execGammaPerSecond;
    public float CoordGammaPerSecond => coordGammaPerSecond;
}
