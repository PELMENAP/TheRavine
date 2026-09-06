using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public interface IGeneticPhenotype
{
    void ApplyGeneticPhenotype(GeneticParameters genetics);
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct GeneticParameters
{
    public float DefaultEvaluation;
    public float Lambda;
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float SoftmaxTemperature;
    public float EntropyRegularization;
    public float LabelSmoothing;
    public float EntropyAlpha;
    public float InitBiasesValues;
    public float GaussianNoise;
    public float ExplorationPrice;
    public float MutationChance;

    public float IdleLowEnergyThreshold;
    public float IdleLongActivityPenaltyStart;
    public float IdleRewardLowEnergy;
    public float IdleRewardOveractive;
    public float EatHealFood;
    public float EatEnergyFood;
    public float EatRewardFood;
    public float EatHealNoFood;
    public float EatEnergyNoFood;
    public float EatRewardNoFood;
    public float StarvationDamage;
    public float StarvationEnergyReturn;
    public float StarvationThreshold;
    public float RestHealRate;
    public float RestEnergyRate;
    public float RestDuration;
    public float RestDeficitThreshold;

    public static readonly (float min, float max, float mutationScale)[] ParameterRanges = {
        (0.1f, 0.9f, 0.1f),      // DefaultEvaluation
        (0.001f, 0.02f, 0.01f),  // Lambda
        (0.005f, 0.1f, 0.1f),    // BaseLearningRate
        (0.5f, 3.0f, 0.3f),      // MaxGradientNorm
        (0.8f, 3.0f, 0.3f),      // SoftmaxTemperature
        (0.01f, 0.2f, 0.1f),     // EntropyRegularization
        (0.1f, 0.5f, 0.1f),      // LabelSmoothing
        (0.05f, 0.3f, 0.1f),     // EntropyAlpha
        (0.01f, 0.3f, 0.1f),     // InitBiasesValues
        (0.01f, 0.1f, 0.05f),    // GaussianNoise
        (0.05f, 0.3f, 0.1f),     // ExplorationPrice
        (0.05f, 0.5f, 0.1f),     // MutationChance
        (0.2f, 0.5f, 0.05f),     // IdleLowEnergyThreshold
        (0.7f, 0.95f, 0.05f),    // IdleLongActivityPenaltyStart
        (0.3f, 0.9f, 0.1f),      // IdleRewardLowEnergy
        (-0.6f, -0.1f, 0.1f),    // IdleRewardOveractive
        (15f, 45f, 5f),          // EatHealFood
        (10f, 30f, 5f),          // EatEnergyFood
        (0.5f, 1f, 0.1f),        // EatRewardFood
        (1f, 10f, 2f),           // EatHealNoFood
        (1f, 10f, 2f),           // EatEnergyNoFood
        (0.1f, 0.6f, 0.1f),      // EatRewardNoFood
        (5f, 25f, 3f),           // StarvationDamage
        (1f, 10f, 2f),           // StarvationEnergyReturn
        (2f, 10f, 2f),           // StarvationThreshold
        (2f, 10f, 2f),           // RestHealRate
        (4f, 15f, 2f),           // RestEnergyRate
        (1f, 6f, 1f),            // RestDuration
        (0.1f, 0.5f, 0.1f),      // RestDeficitThreshold
    };

    public static readonly int GeneCount;

    static GeneticParameters()
    {
        GeneCount = Unsafe.SizeOf<GeneticParameters>() / sizeof(float);
        if (GeneCount != ParameterRanges.Length)
            throw new InvalidOperationException(
                $"GeneticParameters: GeneCount({GeneCount}) != ParameterRanges({ParameterRanges.Length})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<float> AsSpan(ref GeneticParameters p)
        => MemoryMarshal.CreateSpan(
               ref Unsafe.As<GeneticParameters, float>(ref p), GeneCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<float> AsReadOnlySpan(in GeneticParameters p)
        => MemoryMarshal.CreateReadOnlySpan(
               ref Unsafe.As<GeneticParameters, float>(ref Unsafe.AsRef(in p)), GeneCount);

    public static GeneticParameters Default
    {
        get
        {
            GeneticParameters p = default;
            var span = AsSpan(ref p);
            for (int i = 0; i < span.Length; i++)
            {
                var (min, max, _) = ParameterRanges[i];
                span[i] = RavineRandom.RangeFloat(min, max);
            }
            return p;
        }
    }

    public static void Mutate(ref GeneticParameters p)
    {
        var span = AsSpan(ref p);
        float chance = p.MutationChance;

        for (int i = 0; i < span.Length; i++)
        {
            if (RavineRandom.RangeFloat() >= chance) continue;
            var (min, max, scale) = ParameterRanges[i];
            span[i] = Mathf.Clamp(span[i] + RavineRandom.RangeFloat(-1f, 1f) * scale, min, max);
        }
    }

    public static void Crossover(in GeneticParameters a, in GeneticParameters b,
        out GeneticParameters child)
    {
        child = default;

        var sa = AsReadOnlySpan(in a);
        var sb = AsReadOnlySpan(in b);
        var sc = AsSpan(ref child);

        for (int i = 0; i < sc.Length; i++)
            sc[i] = RavineRandom.RangeBool() ? sa[i] : sb[i];

        Mutate(ref child);
    }

    public GeneticParameters GetMutatedGeneticParameters()
    {
        var copy = this;
        Mutate(ref copy);
        return copy;
    }

    public uint ComputeHash()
    {
        var copy = this;
        var bits = MemoryMarshal.Cast<float, uint>(AsSpan(ref copy));

        uint hash = 2166136261u;
        for (int i = 0; i < bits.Length; i++)
            hash = (hash ^ bits[i]) * 16777619u;
        return hash;
    }
}

public struct XorShift32
{
    private uint state;

    public XorShift32(uint seed) => state = seed == 0 ? 0xA5A5A5A5u : seed;

    public uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    public int Range(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
}