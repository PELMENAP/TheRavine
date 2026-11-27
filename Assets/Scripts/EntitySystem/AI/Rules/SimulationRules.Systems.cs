using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField] private bool stressSourcesEnabled;
    [SerializeField] private bool socialReliefEnabled;
    [SerializeField] private bool helpLogEnabled;
    [SerializeField] private bool taskBoardEnabled;
    [SerializeField] private bool depotsEnabled;
    [SerializeField] private bool trailsEnabled;
    [SerializeField] private bool huddlingEnabled;
    [SerializeField] private bool sheltersEnabled;
    [SerializeField] private bool maskStressInput;
    [SerializeField] private bool maskTaskSignal;
    [SerializeField] private bool stressContagionEnabled;
    [SerializeField] private bool playerStressEnabled;

    [SerializeField] private float stressHighThreshold = 0.6f;
    [SerializeField] private float anomalyLoopWindow = 30f;
    [SerializeField] private float anomalyReliefPairThreshold = 1f;
    [SerializeField] private float anomalyMinInvestment = 1e-3f;
    [SerializeField] private int anomalyPairCapacity = 256;
    [SerializeField] private float stressContagionRate = 0.08f;
    [SerializeField] private float stressContagionRadius = 8f;

    public bool StressSourcesEnabled => stressSourcesEnabled;
    public bool SocialReliefEnabled => socialReliefEnabled;
    public bool HelpLogEnabled => helpLogEnabled;
    public bool TaskBoardEnabled => taskBoardEnabled;
    public bool DepotsEnabled => depotsEnabled;
    public bool TrailsEnabled => trailsEnabled;
    public bool HuddlingEnabled => huddlingEnabled;
    public bool SheltersEnabled => sheltersEnabled;
    public bool MaskStressInput => maskStressInput;
    public bool MaskTaskSignal => maskTaskSignal;
    public bool StressContagionEnabled => stressContagionEnabled;
    public bool PlayerStressEnabled => playerStressEnabled;
    public float StressContagionRate => stressContagionRate;
    public float StressContagionRadius => stressContagionRadius;

    public float StressHighThreshold => stressHighThreshold;
    public float AnomalyLoopWindow => anomalyLoopWindow;
    public float AnomalyReliefPairThreshold => anomalyReliefPairThreshold;
    public float AnomalyMinInvestment => anomalyMinInvestment;
    public int AnomalyPairCapacity => anomalyPairCapacity;

    public string SystemFlagsLabel()
    {
        var sb = new System.Text.StringBuilder(96);
        AppendFlag(sb, "stress_sources", stressSourcesEnabled);
        AppendFlag(sb, "social_relief", socialReliefEnabled);
        AppendFlag(sb, "help_log", helpLogEnabled);
        AppendFlag(sb, "task_board", taskBoardEnabled);
        AppendFlag(sb, "depots", depotsEnabled);
        AppendFlag(sb, "trails", trailsEnabled);
        AppendFlag(sb, "huddling", huddlingEnabled);
        AppendFlag(sb, "shelters", sheltersEnabled);
        AppendFlag(sb, "mask_stress_input", maskStressInput);
        AppendFlag(sb, "mask_task_signal", maskTaskSignal);
        AppendFlag(sb, "stress_contagion", stressContagionEnabled);
        AppendFlag(sb, "player_stress", playerStressEnabled);
        AppendFlag(sb, "mate_loci", mateLociEnabled);
        AppendFlag(sb, "knowledge", knowledgeEnabled);
        return sb.ToString();
    }

    private static void AppendFlag(System.Text.StringBuilder sb, string name, bool on)
    {
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(name).Append('=').Append(on ? '1' : '0');
    }

    private void ValidateSystems()
    {
        stressHungerRate = Mathf.Max(stressHungerRate, 0f);
        stressHungerThreshold = Mathf.Clamp01(stressHungerThreshold);
        stressColdRate = Mathf.Max(stressColdRate, 0f);
        lonelinessRadius = Mathf.Max(lonelinessRadius, 0f);
        lonelinessWindow = Mathf.Max(lonelinessWindow, 0f);
        stressLonelyRate = Mathf.Max(stressLonelyRate, 0f);
        stressTaskFailure = Mathf.Max(stressTaskFailure, 0f);
        stressSmoothTau = Mathf.Max(stressSmoothTau, 0f);
        stressStressedEnter = Mathf.Clamp01(stressStressedEnter);
        stressStressedExit = Mathf.Clamp(stressStressedExit, 0f, stressStressedEnter);
        stressHighEnter = Mathf.Clamp(stressHighEnter, stressStressedEnter, 1f);
        stressHighExit = Mathf.Clamp(stressHighExit, stressStressedEnter, stressHighEnter);
        stressDrainMul = Mathf.Max(stressDrainMul, 0f);
        stressRadiusShrink = Mathf.Clamp01(stressRadiusShrink);
        stressDiscountMul = Mathf.Max(stressDiscountMul, 0f);
        stressLrMul = Mathf.Max(stressLrMul, 0f);
        stressLrFloor = Mathf.Clamp01(stressLrFloor);
        stressAbandonChance = Mathf.Clamp01(stressAbandonChance);
        stressHighThreshold = Mathf.Clamp01(stressHighThreshold);
        anomalyLoopWindow = Mathf.Max(anomalyLoopWindow, 0f);
        anomalyReliefPairThreshold = Mathf.Max(anomalyReliefPairThreshold, 0f);
        anomalyMinInvestment = Mathf.Max(anomalyMinInvestment, 0f);
        anomalyPairCapacity = Mathf.Clamp(anomalyPairCapacity, 16, 4096);
        stressContagionRate = Mathf.Max(stressContagionRate, 0f);
        stressContagionRadius = Mathf.Max(stressContagionRadius, 0f);
    }
}
