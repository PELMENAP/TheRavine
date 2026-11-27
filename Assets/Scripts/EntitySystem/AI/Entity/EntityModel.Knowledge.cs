using System;
using Unity.Mathematics;

public partial class EntityModel
{
    private KnowledgeSystem _knowledge;

    private void StepKnowledge(float now, float dayPhase, float detect, in SimulationRules.RulesFrame r)
    {
        var knowledge = _knowledge;
        if (!r.KnowledgeEnabled || knowledge == null || (uint)ManagerIndex >= (uint)knowledge.Capacity) return;

        if (knowledge.ObserveDue(ManagerIndex, now))
        {
            Span<float> x = stackalloc float[KnowledgeSystem.Features];
            x[0] = dayPhase;
            x[1] = _season;
            x[2] = IsAtNest ? 1f : 0f;
            x[3] = _foodValid && detect > 0f ? math.saturate(1f - _foodDistance / detect) : 0f;
            x[4] = Stats.MaxEnergy > 0f ? Stats.En / Stats.MaxEnergy : 0f;
            x[5] = _stressSmooth;
            knowledge.Observe(ManagerIndex, now, x, Stats.En, Stats.MaxEnergy, in r);
        }

        float reward = knowledge.Search(ManagerIndex, now, in r);
        if (reward > 0f) AddExtrinsicReward(reward);

        var mate = _nearest;
        if (mate != null && !mate.IsDisposed && !mate.IsDeathPending && mate.Colony == Colony
            && _nearestDistance >= 0f && _nearestDistance <= r.KnowledgeTeachRadius
            && knowledge.BestNodes(ManagerIndex) > 0 && RavineRandom.RangeFloat() < r.KnowledgeTeachChance)
            knowledge.Teach(ManagerIndex, mate.ManagerIndex, in r);
    }
}
