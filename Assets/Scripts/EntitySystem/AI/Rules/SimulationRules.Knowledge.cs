using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField, FrameField] private bool knowledgeEnabled;
    [SerializeField, FrameField] private float knowledgeObserveInterval = 5f;
    [SerializeField, FrameField] private float knowledgeSearchInterval = 2f;
    [SerializeField, FrameField] private float knowledgeNodeCost = 2f;
    [SerializeField, FrameField] private float knowledgeNoiseFloor = 0.01f;
    [SerializeField, FrameField] private float knowledgeMinGain = 1f;
    [SerializeField, FrameField] private float knowledgeRewardScale = 0.05f;
    [SerializeField, FrameField] private float knowledgeRewardCap = 0.2f;
    [SerializeField, FrameField] private float knowledgeTeachChance = 0.05f;
    [SerializeField, FrameField] private float knowledgeTeachRadius = 4f;
    [SerializeField, FrameField] private float knowledgeCopyError = 0.2f;
    [SerializeField, FrameField] private float knowledgeTargetScale = 100f;
    [SerializeField, FrameField] private float knowledgeConstScale = 0.5f;

    public bool KnowledgeEnabled => knowledgeEnabled;

    private void ValidateKnowledge()
    {
        knowledgeObserveInterval = Mathf.Max(knowledgeObserveInterval, 0.1f);
        knowledgeSearchInterval = Mathf.Max(knowledgeSearchInterval, 0.1f);
        knowledgeNodeCost = Mathf.Max(knowledgeNodeCost, 0f);
        knowledgeNoiseFloor = Mathf.Max(knowledgeNoiseFloor, 1e-9f);
        knowledgeMinGain = Mathf.Max(knowledgeMinGain, 0f);
        knowledgeRewardScale = Mathf.Max(knowledgeRewardScale, 0f);
        knowledgeRewardCap = Mathf.Max(knowledgeRewardCap, 0f);
        knowledgeTeachChance = Mathf.Clamp01(knowledgeTeachChance);
        knowledgeTeachRadius = Mathf.Max(knowledgeTeachRadius, 0f);
        knowledgeCopyError = Mathf.Clamp01(knowledgeCopyError);
        knowledgeTargetScale = Mathf.Max(knowledgeTargetScale, 1e-6f);
        knowledgeConstScale = Mathf.Max(knowledgeConstScale, 0f);
    }
}
