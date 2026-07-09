using System.Runtime.CompilerServices;
using Unity.Mathematics;

public struct FastRandom
{
    private Random random;

    public FastRandom(uint seed)
    {
        random = new Random(seed == 0 ? 1u : seed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat()
    {
        return random.NextFloat();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt()
    {
        return random.NextInt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Range(float min, float max)
    {
        return random.NextFloat(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Next(int max)
    {
        return random.NextInt(0, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Range(int min, int max)
    {
        return random.NextInt(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float2 GetInsideCircle(float radius = 1f)
    {
        float2 p;
        p = new float2(random.NextFloat(-1f, 1f), random.NextFloat(-1f, 1f));
        return p * radius;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public quaternion GetRotation()
    {
        return random.NextQuaternionRotation();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float4 GetColor()
    {
        return new float4(
            random.NextFloat(0f, 1f),
            random.NextFloat(0f, 1f),
            random.NextFloat(0f, 1f),
            random.NextFloat(0.8f, 1f)
        );
    }
}

public static class RavineRandom
{
    private readonly static Random fastRandom = new(1u);
    public static int RangeInt(int min, int max) => fastRandom.NextInt(min, max);
    public static int RangeInt(int max) => fastRandom.NextInt(0, max);
    public static float RangeFloat(float min, float max) => fastRandom.NextFloat(min, max);
    public static float RangeFloat() => fastRandom.NextFloat(0f, 1f);
    public static int Hundred() => RangeInt(0, 100);
    public static UnityEngine.Vector2 GetInsideCircle(float radius = 1)
    {
        return new(
            fastRandom.NextFloat(-1f, 1f) * radius,
            fastRandom.NextFloat(-1f, 1f) * radius
        );
    }

    public static UnityEngine.Vector3 GetInsideSphere(float radius = 1)
    {
        return new(
            fastRandom.NextFloat(-1f, 1f) * radius,
            fastRandom.NextFloat(-1f, 1f) * radius,
            fastRandom.NextFloat(-1f, 1f) * radius
        );
    }
    public static UnityEngine.Color RangeColor()
    {
        return new(
            fastRandom.NextFloat(0f, 1f),
            fastRandom.NextFloat(0f, 1f),
            fastRandom.NextFloat(0f, 1f),
            fastRandom.NextFloat(0.8f, 1f)
        );
    }
    public static bool RangeBool() => fastRandom.NextBool();
}