using UnityEngine;

public static class ObjectGrowthMath
{
    public const float MinSize = 1e-3f;
    public const float MinRatio = 0.05f;
    public const float MaxRatio = 20f;

    public static Vector3 Resolve(Vector3 fromSize, Vector3 toSize, float fromHeight, float toHeight)
    {
        if (IsMeasured(fromSize) && IsMeasured(toSize))
            return ClampRatio(new Vector3(toSize.x / fromSize.x, toSize.y / fromSize.y, toSize.z / fromSize.z));

        if (fromHeight > MinSize && toHeight > MinSize)
            return ClampRatio(Vector3.one * (toHeight / fromHeight));

        return Vector3.one;
    }

    public static Vector3 ApplyFraction(Vector3 ratio, float fraction)
    {
        return new Vector3(
            Mathf.Max(1f, ratio.x * fraction),
            Mathf.Max(1f, ratio.y * fraction),
            Mathf.Max(1f, ratio.z * fraction));
    }

    public static Vector3 SizeAtBase(Vector3 boundsSize, Vector3 localScale, Vector3 baseScale)
    {
        return new Vector3(
            boundsSize.x * Unscale(baseScale.x, localScale.x),
            boundsSize.y * Unscale(baseScale.y, localScale.y),
            boundsSize.z * Unscale(baseScale.z, localScale.z));
    }

    public static Vector3 ScaleAt(Vector3 baseScale, Vector3 ratio, float t)
    {
        return new Vector3(
            baseScale.x * Mathf.LerpUnclamped(1f, ratio.x, t),
            baseScale.y * Mathf.LerpUnclamped(1f, ratio.y, t),
            baseScale.z * Mathf.LerpUnclamped(1f, ratio.z, t));
    }

    private static bool IsMeasured(Vector3 size) => size.x > MinSize && size.y > MinSize && size.z > MinSize;

    private static float Unscale(float baseValue, float current)
        => Mathf.Abs(current) > MinSize && Mathf.Abs(baseValue) > MinSize ? baseValue / current : 1f;

    private static Vector3 ClampRatio(Vector3 ratio)
    {
        return new Vector3(
            Mathf.Clamp(ratio.x, MinRatio, MaxRatio),
            Mathf.Clamp(ratio.y, MinRatio, MaxRatio),
            Mathf.Clamp(ratio.z, MinRatio, MaxRatio));
    }
}
