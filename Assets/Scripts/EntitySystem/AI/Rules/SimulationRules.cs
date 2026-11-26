using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

[CreateAssetMenu(fileName = "SimulationRules", menuName = "Simulation/Rules")]
public sealed partial class SimulationRules : ScriptableObject
{
    public enum EpsilonSchedule { Exponential = 0, PlateauByFitness = 1 }

    private static RulesFrame _frame;
    public static ref readonly RulesFrame Frame => ref _frame;

    public static void CaptureFrame() => _frame = new RulesFrame(Active);

    public static void Bind(SimulationRules rules)
    {
        _active = rules;
        if (rules == null)
        {
            _frame = default;
            return;
        }
        CaptureFrame();
    }

    public float DeathPenaltyMul(DeathCause cause)
        => deathPenaltyByCause != null && (uint)cause < (uint)deathPenaltyByCause.Length ? deathPenaltyByCause[(int)cause] : 1f;

    private static readonly float[] PlanMinBuf = new float[PlanCatalog.Count];
    private static readonly float[] PlanMaxBuf = new float[PlanCatalog.Count];
    private static readonly float[] CasteAffinityLogBuf = new float[(int)Caste.Count * PlanCatalog.Count];
    private static readonly float[] UtilityWeightsBuf = new float[PlanCatalog.Count * UtilityFeatures.Count];
    private static readonly int[]   PlanCasteDomainBuf = new int[PlanCatalog.Count];

    private float[] FillPlanSeconds(float[] source, float fallback, float[] target)
    {
        for (int p = 0; p < target.Length; p++)
            target[p] = source != null && p < source.Length ? source[p] : fallback;
        return target;
    }

    private float[] FillCasteAffinityLog()
    {
        var t = CasteAffinityLogBuf;
        for (int i = 0; i < t.Length; i++)
            t[i] = math.log(math.max(casteAffinity != null && i < casteAffinity.Length ? casteAffinity[i] : 1f, 1e-3f));
        return t;
    }

    private float[] FillUtilityWeights()
    {
        var t = UtilityWeightsBuf;
        for (int i = 0; i < t.Length; i++)
            t[i] = utilityWeights != null && i < utilityWeights.Length ? utilityWeights[i] : 0f;
        return t;
    }

    private int[] FillPlanCasteDomain()
    {
        var t = PlanCasteDomainBuf;
        for (int i = 0; i < t.Length; i++)
            t[i] = planCasteDomain != null && i < planCasteDomain.Length ? planCasteDomain[i] : -1;
        return t;
    }

    private ulong BuildEndogenousForbiddenMask()
    {
        ulong mask = 0UL;
        if (endogenousForbidden == null) return mask;
        for (int i = 0; i < endogenousForbidden.Length; i++)
        {
            int a = (int)endogenousForbidden[i];
            if ((uint)a < 64u) mask |= 1UL << a;
        }
        return mask;
    }

    private FixedList128Bytes<float3> BuildBoidPlanMul()
    {
        var list = new FixedList128Bytes<float3>();
        for (int p = 0; p <= PlanCatalog.Count; p++)
            list.Add(planBoidMul != null && p < planBoidMul.Length ? (float3)planBoidMul[p] : new float3(1f));
        return list;
    }
    public float ColonyChannelTau(int channel)
        => colonyChannelTau != null && (uint)channel < (uint)colonyChannelTau.Length
            ? colonyChannelTau[channel]
            : colonyChannelDefaultTau;
    public int   EpsilonScheduleMode => (int)epsilonScheduleMode;

    public float BiomeFertility(int biome) => BiomeValue(biomeFertility, biome, 1f);
    public float BiomeToxicity(int biome)  => BiomeValue(biomeToxicity, biome, 0f);

    private FoodTraits FoodTraitsFor(FoodKind kind) => new FoodTraits
    {
        Toxicity = KindValue(foodToxicity, kind),
        Size     = KindValue(foodSize, kind),
        Meat     = KindValue(foodMeat, kind),
    };

    private static float KindValue(float[] table, FoodKind kind)
        => table != null && (uint)kind < (uint)table.Length ? table[(int)kind] : 0f;

    private static float BiomeValue(float[] table, int biome, float fallback)
    {
        if (table == null || table.Length == 0) return fallback;
        int i = biome < 0 ? 0 : biome & 3;
        return i < table.Length ? table[i] : fallback;
    }

    public const float LearningRateFloor = 1e-6f;
    public const int   MaxObjectSenseRadius = 8;
    public const int   MaxFireSenseRadius = 32;
    public const float MinAgentTickInterval = 1e-3f;
    public const int   SpeechVocab = 16;
    public const int   SpeechLen = 2;
    public const int   SpeechInbox = 4;
    public const int   SpeechTokenBits = 4;
    public const int   SpeechContextBits = 6;
    public const int   SpeechCreditRing = 256;
    public const int   SpeechCreditListeners = 8;
    public const int   PredispositionSize = 3;
    private const int  PredispositionLayoutCheck = 1 / (PredispositionSize == GeneticParameters.PredispositionCount ? 1 : 0);
    public const int   SpeechShuffleRing = 64;
    private const int  SpeechLayoutCheck = 1 / (SpeechLen * SpeechTokenBits <= 16 && SpeechVocab == 1 << SpeechTokenBits && (SpeechInbox & (SpeechInbox - 1)) == 0 && SpeechContextBits <= 8 ? 1 : 0);

    private void OnValidate()
    {
        if (!(minLearningRate >= LearningRateFloor)) minLearningRate = LearningRateFloor;
        if (!(lrDecayPerSecond >= 0f)) lrDecayPerSecond = 0f;
        if (!(agentTickInterval >= MinAgentTickInterval)) agentTickInterval = MinAgentTickInterval;
        if (!(optimizerBaseLearningRate >= LearningRateFloor)) optimizerBaseLearningRate = LearningRateFloor;
        if (!(optimizerWeightDecay >= 0f)) optimizerWeightDecay = 0f;
        if (maxRing < 1) maxRing = 1;
        if (!(slopeMaxDeg > 0f)) slopeMaxDeg = 1f;
        if (!(relSpan > 0f)) relSpan = 1f;
        if (densityRadiusNear < 1) densityRadiusNear = 1;
        if (densityRadiusMid < 1) densityRadiusMid = 1;
        if (densityRadiusFar < 1) densityRadiusFar = 1;
        if (relativeHeightRadius < 1) relativeHeightRadius = 1;
        if (!(stressNestRecoveryMul >= 1f)) stressNestRecoveryMul = 1f;
        if (!(stressPerceptionNoise >= 0f)) stressPerceptionNoise = 0f;
        if (!(foreignHarmStress >= 0f)) foreignHarmStress = 0f;
        if (!(startleStress >= 0f)) startleStress = 0f;
        if (!(startleCooldown >= 0f)) startleCooldown = 0f;
        if (!(familiarityTau > 0f)) familiarityTau = 1e-3f;
        familiarityGift = familiarityGift >= 0f ? math.min(familiarityGift, 1f) : 0f;
        familiarityCalm = familiarityCalm >= 0f ? math.min(familiarityCalm, 1f) : 0f;
        if (!(familiarityHuntMul >= 0f)) familiarityHuntMul = 0f;
        if (!(curiosityEnergyGain >= 0f)) curiosityEnergyGain = 0f;
        curiosityEnergyFloor = curiosityEnergyFloor >= 0f ? math.min(curiosityEnergyFloor, 0.99f) : 0f;
        if (!(curiosityRarityWeight >= 0f)) curiosityRarityWeight = 0f;
        if (!(quorumMinDistance >= 0f)) quorumMinDistance = 0f;
        if (!(quorumSlopeLimit > 0f)) quorumSlopeLimit = 1e-3f;
        if (!(quorumDepressionMul >= 0f)) quorumDepressionMul = 0f;
        if (!(quorumFoodNorm > 0f)) quorumFoodNorm = 1e-3f;
        if (!(soldierRunSpeedMul > 0f)) soldierRunSpeedMul = 1f;
        if (!(predatorFearMul >= 0f)) predatorFearMul = 0f;
        if (!(predatorPackSpread >= 0f)) predatorPackSpread = 0f;
        if (!(pressureTau > 0f)) pressureTau = 1e-3f;
        if (!(pressureFromDamage >= 0f)) pressureFromDamage = 0f;
        if (!(pressureFromKill >= 0f)) pressureFromKill = 0f;
        if (!(pressureSoldierGain >= 0f)) pressureSoldierGain = 0f;
        pressureQuorumRelief = pressureQuorumRelief >= 0f ? math.min(pressureQuorumRelief, 1f) : 0f;
        if (!(packRadius >= 0f)) packRadius = 0f;
        if (!(packRate >= 0f)) packRate = 0f;
        if (!(soldierDetectMul > 0f)) soldierDetectMul = 1f;
        if (!(nestExitRadiusMul >= 1f)) nestExitRadiusMul = 1f;
        soilReturnFraction = soilReturnFraction >= 0f ? math.min(soilReturnFraction, 1f) : 0f;
        if (!(soilPerDepletion >= 0f)) soilPerDepletion = 0f;
        basalEmitFraction  = basalEmitFraction >= 0f ? math.min(basalEmitFraction, 1f) : 0f;
        warmthDrawFraction = warmthDrawFraction >= 0f ? math.min(warmthDrawFraction, 1f) : 0f;
        if (!(warmthCellSize > 0f)) warmthCellSize = 1f;
        if (warmthBuckets < 1) warmthBuckets = 1;
        if (!(warmthDecayPerSecond >= 0f)) warmthDecayPerSecond = 0f;
        if (!(simulationLogIntervalSeconds >= 1f)) simulationLogIntervalSeconds = 1f;
        if (!(ltcTauMin > 0f)) ltcTauMin = DelayedPerceptron.TauEpsilon;
        if (!(ltcTauMax >= ltcTauMin)) ltcTauMax = float.MaxValue;
        if (!(nurseLeashRate >= 0f)) nurseLeashRate = 0f;
        if (!(nurseryRate >= 0f)) nurseryRate = 0f;
        if (!(tetherRate >= 0f)) tetherRate = 0f;
        if (!(nightHomeRate >= 0f)) nightHomeRate = 0f;
        objectSenseRadius = math.clamp(objectSenseRadius, 0, MaxObjectSenseRadius);
        fireSenseRadius = math.clamp(fireSenseRadius, 0, MaxFireSenseRadius);
        if (!(objectReach >= 0f)) objectReach = 0f;
        if (!(agentEditRate >= 0f)) agentEditRate = 0f;
        if (!(agentEditBurst >= 0f)) agentEditBurst = 0f;
        if (agentEditsPerFrame < 0) agentEditsPerFrame = 0;
        if (!(heldObjectSpeedMul > 0f)) heldObjectSpeedMul = 1f;
        if (!(dangerDirectionMinMass >= 0f)) dangerDirectionMinMass = 0f;
        ValidateSkills();
        ValidateCodex();
        ValidateSystems();
        ValidateSocial();
        ValidateCulture();
    }

    private static SimulationRules _active;

    public static SimulationRules Active
    {
        get
        {
            if (_active == null) ThrowNotBound();
            return _active;
        }
    }

    private static void ThrowNotBound() =>
        throw new System.InvalidOperationException(
            "SimulationRules asset is not bound: assign a SimulationRules asset to EntityManager.rules (Assets/Scripts/EntitySystem/AI/SimulationRules.asset)");
}
