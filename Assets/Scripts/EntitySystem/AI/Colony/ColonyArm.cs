using Unity.Mathematics;

public enum ColonyArm : byte
{
    Control,
    SpeechMuted,
    SpeechShuffled,
    NullPolicy,
    Count,
}

public static class ColonyArms
{
    private static readonly string[] Names = { "control", "speech_muted", "speech_shuffled", "null_policy" };

    public static string Name(ColonyArm arm) => (uint)arm < (uint)Names.Length ? Names[(int)arm] : Names[0];

    public static ColonyArm FromByte(byte value) => value < (byte)ColonyArm.Count ? (ColonyArm)value : ColonyArm.Control;

    public static ColonyArm Draw(SimulationRules rules, bool wild)
    {
        if (wild) return ColonyArm.Control;

        float muted    = math.saturate(rules.ArmSpeechMutedFraction);
        float shuffled = math.saturate(rules.ArmSpeechShuffledFraction);
        float nullp    = math.saturate(rules.ArmNullPolicyFraction);
        if (muted + shuffled + nullp <= 0f) return ColonyArm.Control;

        float u = RavineRandom.RangeFloat();
        if (u < muted) return ColonyArm.SpeechMuted;
        u -= muted;
        if (u < shuffled) return ColonyArm.SpeechShuffled;
        u -= shuffled;
        if (u < nullp) return ColonyArm.NullPolicy;
        return ColonyArm.Control;
    }
}
