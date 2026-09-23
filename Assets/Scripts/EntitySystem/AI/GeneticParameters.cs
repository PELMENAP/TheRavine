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
    public float MoveSpeedMul;
    public float BaseLearningRate;
    public float MaxGradientNorm;
    public float SoftmaxTemperature;
    public float EntropyRegularization;
    public float MaxEnergyMul;
    public float EntropyAlpha;
    public float MetabolismMul;
    public float GaussianNoise;
    public float MutationChance;
    public float DetectionRadiusMul;
    public float Sharpness;

    public const int IdxMoveSpeedMul = 0;
    public const int IdxBaseLearningRate = 1;
    public const int IdxMaxGradientNorm = 2;
    public const int IdxSoftmaxTemperature = 3;
    public const int IdxEntropyRegularization = 4;
    public const int IdxMaxEnergyMul = 5;
    public const int IdxEntropyAlpha = 6;
    public const int IdxMetabolismMul = 7;
    public const int IdxGaussianNoise = 8;
    public const int IdxMutationChance = 9;
    public const int IdxDetectionRadiusMul = 10;
    public const int IdxSharpness = 11;

    public static readonly (float min, float max, float mutationScale)[] ParameterRanges = {
        (0.7f, 1.4f, 0.08f),
        (0.005f, 0.1f, 0.1f),
        (0.5f, 3.0f, 0.3f),
        (0.8f, 3.0f, 0.3f),
        (0.01f, 0.2f, 0.1f),
        (0.7f, 1.5f, 0.08f),
        (0.05f, 0.3f, 0.1f),
        (0.7f, 1.4f, 0.08f),
        (0.01f, 0.1f, 0.05f),
        (0.05f, 0.5f, 0.1f),
        (0.7f, 1.5f, 0.08f),
        (0.15f, 1.5f, 0.15f),
    };

    public static bool IsPhenotypic(int index)
        => index == IdxMoveSpeedMul || index == IdxMaxEnergyMul
        || index == IdxMetabolismMul || index == IdxDetectionRadiusMul;

    public static void CopyLearningGenes(in GeneticParameters source, ref GeneticParameters target)
    {
        var src = AsReadOnlySpan(in source);
        var dst = AsSpan(ref target);
        for (int i = 0; i < dst.Length; i++)
            if (!IsPhenotypic(i)) dst[i] = src[i];
    }

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