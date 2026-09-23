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

        public readonly float MaxCycleDt;
        public readonly float ModifierDecayTau;

        public readonly float DangerLowFraction;
        public readonly float DangerMidFraction;
        public readonly float DangerCritFraction;
        public readonly float BreedMarginFraction;

        public readonly float CodonsPerSecond;
        public readonly int   MaxCodonsPerCycle;
        public readonly int   DormantCodons;
        public readonly float ContactRadius;
        public readonly float IntegrityDecayPerSecond;
        public readonly float IntegrityMutationScale;
        public readonly float IntegrityRemoveThreshold;

        public readonly float RndNormAlpha;
        public readonly int   EpsilonScheduleMode;

        public readonly float ActionTraceHorizon;
        public readonly float ActionTraceInvLogNorm;

        public readonly float BlockedSpeedThreshold;
        public readonly float BlockedSeconds;

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

            MaxCycleDt       = r.MaxCycleDt;
            ModifierDecayTau = r.ModifierDecayTau;

            DangerLowFraction   = r.dangerLowFraction;
            DangerMidFraction   = r.dangerMidFraction;
            DangerCritFraction  = r.dangerCritFraction;
            BreedMarginFraction = r.breedMarginFraction;

            CodonsPerSecond          = r.codonsPerSecond;
            MaxCodonsPerCycle        = r.maxCodonsPerCycle;
            DormantCodons            = r.dormantCodons;
            ContactRadius            = r.contactRadius;
            IntegrityDecayPerSecond  = r.integrityDecayPerSecond;
            IntegrityMutationScale   = r.integrityMutationScale;
            IntegrityRemoveThreshold = r.integrityRemoveThreshold;

            RndNormAlpha        = r.rndNormAlpha;
            EpsilonScheduleMode = (int)r.epsilonScheduleMode;

            ActionTraceHorizon    = r.actionTraceHorizon;
            ActionTraceInvLogNorm = 1f / Mathf.Log(1f + Mathf.Max(r.actionTraceHorizon, 1e-3f));

            BlockedSpeedThreshold = r.blockedSpeedThreshold;
            BlockedSeconds        = r.blockedSeconds;
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

    [SerializeField] private float eatHealFraction = 0f;
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

    [SerializeField] private float maxCycleDt = 3f;
    [SerializeField] private float modifierDecayTau = 85f;
    [SerializeField] private float eatRange = 2f;
    [SerializeField] private float infeasibleActionHealthPenalty = 0f;
    [SerializeField] private float threatenDuration = 0.8f;
    [SerializeField] private float shareFoodDuration = 0.5f;
    [SerializeField] private float commandWatchdogGrace = 0.1f;
    [SerializeField] private float rewardStdFloor = 0.05f;

    public float MaxCycleDt                    => maxCycleDt;
    public float ModifierDecayTau              => modifierDecayTau;
    public float EatRange                      => eatRange;
    public float InfeasibleActionHealthPenalty => infeasibleActionHealthPenalty;
    public float ThreatenDuration              => threatenDuration;
    public float ShareFoodDuration             => shareFoodDuration;
    public float CommandWatchdogGrace          => commandWatchdogGrace;
    public float RewardStdFloor                => rewardStdFloor;

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

    public float EatHealFraction => eatHealFraction;
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

    public enum EpsilonSchedule { Exponential = 0, PlateauByFitness = 1 }

    [SerializeField] private float idleRewardGainNorm = 0.02f;
    [SerializeField] private float restHealEnergyCost = 0.5f;
    [SerializeField] private float speechEnergyCost = 5f;
    [SerializeField] private float threatenEnergyCost = 3f;

    [SerializeField] private float dangerLowFraction   = 0.5f;
    [SerializeField] private float dangerMidFraction   = 0.25f;
    [SerializeField] private float dangerCritFraction  = 0.1f;
    [SerializeField] private float breedMarginFraction = 0.5f;

    [SerializeField] private float shareFoodMinHealthFraction  = 0.8f;
    [SerializeField] private float shareFoodKeepHealthFraction = 0.6f;
    [SerializeField] private float shareFoodTransferFraction   = 0.2f;
    [SerializeField] private float shareFoodVictimRatio        = 0.8f;

    [SerializeField] private float fleeRewardNoTarget   = 0.3f;
    [SerializeField] private float fleeRewardTargetGone = 0.5f;
    [SerializeField] private float fleeDistanceMul      = 1.5f;
    [SerializeField] private float fleeMaxDuration      = 2f;

    [SerializeField] private float rememberPointMinSpacing = 10f;
    [SerializeField] private float rememberPointRewardNew  = 0.65f;
    [SerializeField] private float rememberPointRewardDup  = 0.3f;

    [SerializeField] private float goToPointRewardNoPoints = 0f;
    [SerializeField] private float goToPointRewardArrived  = 0.55f;
    [SerializeField] private float goToPointMaxDuration    = 5f;

    [SerializeField] private float speechReward = 0.55f;

    [SerializeField] private float mimicRewardNoTarget     = 0.2f;
    [SerializeField] private float mimicRewardBase         = 0.3f;
    [SerializeField] private float mimicRewardEntropyScale = 0.2f;

    [SerializeField] private float threatenRewardNoTarget = 0.2f;
    [SerializeField] private float threatenRewardTooFar   = 0.15f;
    [SerializeField] private float threatenRewardClose    = 0.6f;
    [SerializeField] private float threatenRewardFar      = 0.4f;
    [SerializeField] private float threatenRangeMul       = 2f;

    [SerializeField] private float geneSpeedCostExponent    = 2f;
    [SerializeField] private float geneEnergyCapacityUpkeep = 0.5f;
    [SerializeField] private float geneDetectionUpkeep      = 0.3f;
    [SerializeField] private float geneMinBasalMul          = 0.3f;
    [SerializeField] private float initBiasRange            = 0.1f;
    [SerializeField] private bool  periodicGenotypeOverwrite = false;

    [SerializeField] private float codonsPerSecond                = 1f;
    [SerializeField] private int   maxCodonsPerCycle              = 4;
    [SerializeField] private int   dormantCodons                  = 8;
    [SerializeField] private float contactRadius                  = 3f;
    [SerializeField] private float integrityDecayPerSecond        = 0.002f;
    [SerializeField] private float integrityMutationScale         = 0.05f;
    [SerializeField] private float integrityRemoveThreshold       = 0.1f;
    [SerializeField] private float verticalTransmissionChance     = 0.5f;
    [SerializeField] private float verticalEndogenousMutationRate = 0.02f;
    [SerializeField] private float verticalTableMutationChance    = 0.05f;
    [SerializeField] private int   virologyMaxSegments            = 32;

    [SerializeField] private int   rndSamplesPerSweep = 128;
    [SerializeField] private float rndLearningRate    = 0.01f;
    [SerializeField] private float rndNormAlpha       = 0.01f;

    [SerializeField] private EpsilonSchedule epsilonScheduleMode = EpsilonSchedule.Exponential;
    [SerializeField] private float epsilonPlateauFitnessThreshold = 5f;
    [SerializeField] private float epsilonPlateauDrop             = 0.7f;
    [SerializeField] private float epsilonPlateauMinScale         = 0.1f;
    [SerializeField] private float epsilonPlateauThresholdGrowth  = 1.2f;
    [SerializeField] private float epsilonPlateauThresholdStep    = 0f;

    [SerializeField] private float actionTraceHorizon = 120f;

    [SerializeField] private float reservoirSpectralRadius = 0.9f;

    [SerializeField] private float blockedSpeedThreshold = 0.15f;
    [SerializeField] private float blockedSeconds        = 0.75f;

    public float IdleRewardGainNorm => idleRewardGainNorm;
    public float RestHealEnergyCost => restHealEnergyCost;
    public float SpeechEnergyCost   => speechEnergyCost;
    public float ThreatenEnergyCost => threatenEnergyCost;

    public float ShareFoodMinHealthFraction  => shareFoodMinHealthFraction;
    public float ShareFoodKeepHealthFraction => shareFoodKeepHealthFraction;
    public float ShareFoodTransferFraction   => shareFoodTransferFraction;
    public float ShareFoodVictimRatio        => shareFoodVictimRatio;

    public float FleeRewardNoTarget   => fleeRewardNoTarget;
    public float FleeRewardTargetGone => fleeRewardTargetGone;
    public float FleeDistanceMul      => fleeDistanceMul;
    public float FleeMaxDuration      => fleeMaxDuration;

    public float RememberPointMinSpacing => rememberPointMinSpacing;
    public float RememberPointRewardNew  => rememberPointRewardNew;
    public float RememberPointRewardDup  => rememberPointRewardDup;

    public float GoToPointRewardNoPoints => goToPointRewardNoPoints;
    public float GoToPointRewardArrived  => goToPointRewardArrived;
    public float GoToPointMaxDuration    => goToPointMaxDuration;

    public float SpeechReward => speechReward;

    public float MimicRewardNoTarget     => mimicRewardNoTarget;
    public float MimicRewardBase         => mimicRewardBase;
    public float MimicRewardEntropyScale => mimicRewardEntropyScale;

    public float ThreatenRewardNoTarget => threatenRewardNoTarget;
    public float ThreatenRewardTooFar   => threatenRewardTooFar;
    public float ThreatenRewardClose    => threatenRewardClose;
    public float ThreatenRewardFar      => threatenRewardFar;
    public float ThreatenRangeMul       => threatenRangeMul;

    public float GeneSpeedCostExponent     => geneSpeedCostExponent;
    public float GeneEnergyCapacityUpkeep  => geneEnergyCapacityUpkeep;
    public float GeneDetectionUpkeep       => geneDetectionUpkeep;
    public float GeneMinBasalMul           => geneMinBasalMul;
    public float InitBiasRange             => initBiasRange;
    public bool  PeriodicGenotypeOverwrite => periodicGenotypeOverwrite;

    public float VerticalTransmissionChance     => verticalTransmissionChance;
    public float VerticalEndogenousMutationRate => verticalEndogenousMutationRate;
    public float VerticalTableMutationChance    => verticalTableMutationChance;
    public int   VirologyMaxSegments            => virologyMaxSegments;

    public int   RndSamplesPerSweep => rndSamplesPerSweep;
    public float RndLearningRate    => rndLearningRate;

    public int   EpsilonScheduleMode            => (int)epsilonScheduleMode;
    public float EpsilonPlateauFitnessThreshold => epsilonPlateauFitnessThreshold;
    public float EpsilonPlateauDrop             => epsilonPlateauDrop;
    public float EpsilonPlateauMinScale         => epsilonPlateauMinScale;
    public float EpsilonPlateauThresholdGrowth  => epsilonPlateauThresholdGrowth;
    public float EpsilonPlateauThresholdStep    => epsilonPlateauThresholdStep;

    public float ReservoirSpectralRadius => reservoirSpectralRadius;

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


