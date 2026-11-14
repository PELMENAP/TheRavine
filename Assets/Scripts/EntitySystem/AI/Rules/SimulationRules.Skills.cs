using UnityEngine;
using Unity.Mathematics;

public sealed partial class SimulationRules
{
    public const float DefaultSkillK4Quantile = 0.15f;
    public const int DefaultSkillIslandDropMax = 2;

    [SerializeField] private float skillTalentMin = 0.25f;
    [SerializeField] private float skillTalentDecay = 2.5f;
    [SerializeField] private float skillTalentEuclidShare = 0.65f;
    [SerializeField] private float skillPhiRefLevel = 6f;
    [SerializeField] private float skillEdgeCostScale = 3.5f;
    [SerializeField] private float skillEdgeCostExp = 1.35f;
    [SerializeField] private float skillClique3Boost = 1.10f;
    [SerializeField] private float skillClique4Boost = 1.40f;
    [SerializeField] private float skillCycleMinFactor = 0.25f;
    [SerializeField] private float skillCycleScale = 0.35f;
    [SerializeField] private float skillLogicScale = 0.60f;
    [SerializeField] private float skillLogicOffset = 10f;
    [SerializeField] private float skillPruneFocusScale = 0.45f;
    [SerializeField] private float skillDecayRate = 1f / 600f;
    [SerializeField] private float skillSavingsFloor = 0.25f;
    [SerializeField] private float skillXpLevelOffset = 4f;
    [SerializeField] private float skillSolverNormLevel = 100f;
    [SerializeField] private float skillSolverIslandBonus = 0.5f;
    [SerializeField] private float skillPruneUnusedLevel = 8f;
    [SerializeField] private int skillPruneMinSize = 4;
    [SerializeField] private float skillSolverInterval = 30f;
    [SerializeField] private float skillSweepInterval = 5f;
    [SerializeField] private int skillIslandDropMax = DefaultSkillIslandDropMax;
    [SerializeField] private float skillForestGeneRange = 0.25f;
    [SerializeField] private float skillK4Quantile = DefaultSkillK4Quantile;
    [SerializeField] private float skillReplicateInterval = 1f;
    [SerializeField] private float skillLevelFixedScale = 100f;

    public float SkillTalentMin => skillTalentMin;
    public float SkillTalentDecay => skillTalentDecay;
    public float SkillTalentEuclidShare => skillTalentEuclidShare;
    public float SkillPhiRefLevel => skillPhiRefLevel;
    public float SkillEdgeCostScale => skillEdgeCostScale;
    public float SkillEdgeCostExp => skillEdgeCostExp;
    public float SkillClique3Boost => skillClique3Boost;
    public float SkillClique4Boost => skillClique4Boost;
    public float SkillCycleMinFactor => skillCycleMinFactor;
    public float SkillCycleScale => skillCycleScale;
    public float SkillLogicScale => skillLogicScale;
    public float SkillLogicOffset => skillLogicOffset;
    public float SkillPruneFocusScale => skillPruneFocusScale;
    public float SkillDecayRate => skillDecayRate;
    public float SkillSavingsFloor => skillSavingsFloor;
    public float SkillXpLevelOffset => skillXpLevelOffset;
    public float SkillSolverNormLevel => skillSolverNormLevel;
    public float SkillSolverIslandBonus => skillSolverIslandBonus;
    public float SkillPruneUnusedLevel => skillPruneUnusedLevel;
    public int SkillPruneMinSize => skillPruneMinSize;
    public float SkillSolverInterval => skillSolverInterval;
    public float SkillSweepInterval => skillSweepInterval;
    public int SkillIslandDropMax => skillIslandDropMax;
    public float SkillForestGeneRange => skillForestGeneRange;
    public float SkillK4Quantile => skillK4Quantile;
    public float SkillReplicateInterval => skillReplicateInterval;
    public float SkillLevelFixedScale => skillLevelFixedScale;

    public static bool IsBound => _active != null;

    private void ValidateSkills()
    {
        skillTalentMin = math.clamp(skillTalentMin, 0f, 1f);
        if (!(skillTalentDecay >= 0f)) skillTalentDecay = 0f;
        skillTalentEuclidShare = math.clamp(skillTalentEuclidShare, 0f, 1f);
        if (!(skillPhiRefLevel > 0f)) skillPhiRefLevel = 1e-3f;
        if (!(skillEdgeCostScale > 0f)) skillEdgeCostScale = 1e-3f;
        if (!(skillEdgeCostExp > 0f)) skillEdgeCostExp = 1e-3f;
        if (!(skillClique3Boost >= 1f)) skillClique3Boost = 1f;
        if (!(skillClique4Boost >= 1f)) skillClique4Boost = 1f;
        skillCycleMinFactor = math.clamp(skillCycleMinFactor, 0f, 1f);
        if (!(skillCycleScale >= 0f)) skillCycleScale = 0f;
        if (!(skillLogicScale >= 0f)) skillLogicScale = 0f;
        if (!(skillLogicOffset > 0f)) skillLogicOffset = 1e-3f;
        if (!(skillPruneFocusScale >= 0f)) skillPruneFocusScale = 0f;
        if (!(skillDecayRate >= 0f)) skillDecayRate = 0f;
        skillSavingsFloor = math.clamp(skillSavingsFloor, 0f, 1f);
        if (!(skillXpLevelOffset > 0f)) skillXpLevelOffset = 1e-3f;
        if (!(skillSolverNormLevel > 0f)) skillSolverNormLevel = 1e-3f;
        if (!(skillSolverIslandBonus >= 0f)) skillSolverIslandBonus = 0f;
        if (!(skillPruneUnusedLevel >= 0f)) skillPruneUnusedLevel = 0f;
        if (skillPruneMinSize < 1) skillPruneMinSize = 1;
        if (!(skillSolverInterval >= 1f)) skillSolverInterval = 1f;
        if (!(skillSweepInterval >= 1f)) skillSweepInterval = 1f;
        skillIslandDropMax = math.clamp(skillIslandDropMax, 0, 2);
        skillForestGeneRange = math.clamp(skillForestGeneRange, 0f, 1f);
        skillK4Quantile = math.clamp(skillK4Quantile, 0f, 1f);
        if (!(skillReplicateInterval >= 0.1f)) skillReplicateInterval = 0.1f;
        skillLevelFixedScale = math.clamp(skillLevelFixedScale, 1f, 100f);
    }
}
