using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class SimulationRules
{
    public readonly partial struct RulesFrame
    {
        public float BiomeFertility(int biome) => BiomeValue(BiomeFertilityTable, biome, 1f);
        public float BiomeToxicity(int biome)  => BiomeValue(BiomeToxicityTable, biome, 0f);
        public FoodTraits TraitsOf(FoodKind kind) => kind switch
        {
            FoodKind.Toxic => FoodTraitsToxic,
            FoodKind.Large => FoodTraitsLarge,
            FoodKind.Meat  => FoodTraitsMeat,
            _              => FoodTraitsPlant,
        };
    }

    private readonly struct FrameDerived
    {
        public readonly int GradientConflictInterval;
        public readonly float NoveltyFastRate;
        public readonly float NoveltySlowRate;
        public readonly float RareActionChance;
        public readonly float SpeechLoudness;
        public readonly float SpeechMinRadiusFraction;
        public readonly float SpeechAttenuationExp;
        public readonly float SpeechFreshSeconds;
        public readonly float SpeechSyllableSeconds;
        public readonly float SpeechCooldown;
        public readonly float SpeechCreditWindow;
        public readonly float SpeechCreditBaselineAlpha;
        public readonly float SpeechDecodeLossWeight;
        public readonly float SpeechMuteFraction;
        public readonly int EpsilonScheduleMode;
        public readonly float ActionTraceInvLogNorm;
        public readonly int AlarmCryToken;
        public readonly ulong EndogenousForbiddenMask;
        public readonly FixedList128Bytes<float3> BoidPlanMul;
        public readonly float3 BoidNightMul;
        public readonly float QuorumMaxDistance;
        public readonly FoodTraits FoodTraitsPlant;
        public readonly FoodTraits FoodTraitsToxic;
        public readonly FoodTraits FoodTraitsLarge;
        public readonly FoodTraits FoodTraitsMeat;
        public readonly int ObjectSenseRadius;
        public readonly float ObjectFuelHeatPerUnit;
        public readonly int FireSenseRadius;
        public readonly float[] PlanMinSecondsByPlan;
        public readonly float[] PlanMaxSecondsByPlan;
        public readonly float[] CasteAffinityLog;
        public readonly float[] UtilityWeights;
        public readonly int[] PlanCasteDomain;

        public FrameDerived(SimulationRules r)
        {
            GradientConflictInterval = math.max(r.gradientConflictInterval, 1);
            NoveltyFastRate = math.saturate(r.noveltyFastRate);
            NoveltySlowRate = math.saturate(r.noveltySlowRate);
            RareActionChance = math.saturate(r.rareActionChance);
            SpeechLoudness = math.clamp(r.speechLoudness, 0, 255);
            SpeechMinRadiusFraction = math.saturate(r.speechMinRadiusFraction);
            SpeechAttenuationExp = math.max(r.speechAttenuationExp, 0f);
            SpeechFreshSeconds = math.max(r.speechFreshSeconds, 1e-3f);
            SpeechSyllableSeconds = math.max(r.speechSyllableSeconds, 1e-3f);
            SpeechCooldown = math.max(r.speechCooldown, 0f);
            SpeechCreditWindow = math.max(r.speechCreditWindow, 0f);
            SpeechCreditBaselineAlpha = math.saturate(r.speechCreditBaselineAlpha);
            SpeechDecodeLossWeight = math.max(r.speechDecodeLossWeight, 0f);
            SpeechMuteFraction = math.saturate(r.speechMuteFraction);
            EpsilonScheduleMode = (int)r.epsilonScheduleMode;
            ActionTraceInvLogNorm = 1f / math.log(1f + math.max(r.actionTraceHorizon, 1e-3f));
            AlarmCryToken = math.clamp(r.alarmCryToken, 0, SpeechVocab - 1);
            EndogenousForbiddenMask = r.BuildEndogenousForbiddenMask();
            BoidPlanMul = r.BuildBoidPlanMul();
            BoidNightMul = r.boidNightMul;
            QuorumMaxDistance = math.max(r.quorumMinDistance, r.quorumMaxDistance);
            FoodTraitsPlant = r.FoodTraitsFor(FoodKind.Plant);
            FoodTraitsToxic = r.FoodTraitsFor(FoodKind.Toxic);
            FoodTraitsLarge = r.FoodTraitsFor(FoodKind.Large);
            FoodTraitsMeat = r.FoodTraitsFor(FoodKind.Meat);
            ObjectSenseRadius = math.clamp(r.objectSenseRadius, 0, MaxObjectSenseRadius);
            ObjectFuelHeatPerUnit = math.max(0f, r.objectFuelHeatPerUnit);
            FireSenseRadius = math.clamp(r.fireSenseRadius, 0, MaxFireSenseRadius);
            PlanMinSecondsByPlan = r.FillPlanSeconds(r.planMinSecondsByPlan, r.planMinSeconds, PlanMinBuf);
            PlanMaxSecondsByPlan = r.FillPlanSeconds(r.planMaxSecondsByPlan, r.planMaxSeconds, PlanMaxBuf);
            CasteAffinityLog = r.FillCasteAffinityLog();
            UtilityWeights = r.FillUtilityWeights();
            PlanCasteDomain = r.FillPlanCasteDomain();
        }
    }
}
