using Unity.Mathematics;

public enum ColonyArm : byte
{
    Control,
    SpeechMuted,
    SpeechShuffled,
    NullPolicy,
    BoundedLtc,
    ProgressNovelty,
    OrthogonalReservoir,
    Count,
}

public static class ColonyArms
{
    private static readonly string[] Names =
    {
        "control", "speech_muted", "speech_shuffled", "null_policy", "bounded_ltc", "progress_novelty", "orthogonal_reservoir",
    };

    public static string Name(ColonyArm arm) => (uint)arm < (uint)Names.Length ? Names[(int)arm] : Names[0];

    public static ColonyArm Draw(SimulationRules rules, bool wild)
    {
        if (wild) return ColonyArm.Control;

        float total = 0f;
        for (int a = (int)ColonyArm.Control + 1; a < (int)ColonyArm.Count; a++) total += math.saturate(rules.ArmFraction((ColonyArm)a));
        if (total <= 0f) return ColonyArm.Control;

        float u = RavineRandom.RangeFloat();
        for (int a = (int)ColonyArm.Control + 1; a < (int)ColonyArm.Count; a++)
        {
            float share = math.saturate(rules.ArmFraction((ColonyArm)a));
            if (u < share) return (ColonyArm)a;
            u -= share;
        }
        return ColonyArm.Control;
    }
}
