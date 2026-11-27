using System;
using Unity.Mathematics;

public static class KnowledgeMath
{
    public const int MaxNodes = 16;

    public const byte OpConst = 0;
    public const byte OpVar   = 1;
    public const byte OpAdd   = 2;
    public const byte OpSub   = 3;
    public const byte OpMul   = 4;
    public const byte OpMin   = 5;
    public const byte OpMax   = 6;
    public const byte OpLess  = 7;
    public const int  OpCount = 8;

    public static bool IsLeaf(byte op) => op <= OpVar;

    public static bool IsValid(ReadOnlySpan<byte> ops, ReadOnlySpan<float> args, int count, int features)
    {
        if (count <= 0 || count > MaxNodes || count > ops.Length || count > args.Length) return false;
        int depth = 0;
        for (int i = 0; i < count; i++)
        {
            byte op = ops[i];
            if (op >= OpCount) return false;
            if (op == OpVar && ((int)args[i] < 0 || (int)args[i] >= features)) return false;
            if (op == OpConst && !math.isfinite(args[i])) return false;
            depth += IsLeaf(op) ? 1 : -1;
            if (depth < 1) return false;
        }
        return depth == 1;
    }

    public static float Evaluate(ReadOnlySpan<byte> ops, ReadOnlySpan<float> args, int count, ReadOnlySpan<float> x)
    {
        Span<float> stack = stackalloc float[MaxNodes];
        int sp = 0;
        for (int i = 0; i < count; i++)
        {
            byte op = ops[i];
            if (op == OpConst) { stack[sp++] = args[i]; continue; }
            if (op == OpVar) { stack[sp++] = x[(int)args[i]]; continue; }
            if (sp < 2) return float.NaN;
            float b = stack[--sp];
            float a = stack[sp - 1];
            stack[sp - 1] = op switch
            {
                OpAdd => a + b,
                OpSub => a - b,
                OpMul => a * b,
                OpMin => math.min(a, b),
                OpMax => math.max(a, b),
                _     => a < b ? 1f : 0f,
            };
        }
        return sp == 1 && math.isfinite(stack[0]) ? stack[0] : float.NaN;
    }

    public static int SubtreeStart(ReadOnlySpan<byte> ops, int end)
    {
        int need = 1;
        int i = end;
        while (i >= 0)
        {
            need += IsLeaf(ops[i]) ? -1 : 1;
            if (need == 0) return i;
            i--;
        }
        return 0;
    }

    public static float ModelCost(int count, float nodeCost) => count * nodeCost;

    public static float DataCost(float sumSquared, int samples, float noiseFloor)
        => samples > 0 ? samples * math.log2(1f + sumSquared / samples / math.max(noiseFloor, 1e-12f)) : 0f;

    public static int Mutate(Span<byte> ops, Span<float> args, int count, int features, float constScale)
    {
        if (count <= 0)
        {
            ops[0] = OpConst;
            args[0] = 0f;
            return 1;
        }

        int k = RavineRandom.RangeInt(0, count);
        switch (RavineRandom.RangeInt(0, 5))
        {
            case 0:
                for (int t = 0; t < count; t++)
                {
                    int i = (k + t) % count;
                    if (ops[i] != OpConst) continue;
                    args[i] += RavineRandom.RangeFloat(-1f, 1f) * constScale;
                    return count;
                }
                return Grow(ops, args, count, k, features, constScale);
            case 1:
                for (int t = 0; t < count; t++)
                {
                    int i = (k + t) % count;
                    if (IsLeaf(ops[i])) continue;
                    ops[i] = (byte)RavineRandom.RangeInt(OpAdd, OpCount);
                    return count;
                }
                return Grow(ops, args, count, k, features, constScale);
            case 2:
                if (!IsLeaf(ops[k])) return count;
                RandomLeaf(ref ops[k], ref args[k], features, constScale);
                return count;
            case 3:
                return Grow(ops, args, count, k, features, constScale);
            default:
                return Shrink(ops, args, count, k);
        }
    }

    private static int Grow(Span<byte> ops, Span<float> args, int count, int k, int features, float constScale)
    {
        if (count + 2 > MaxNodes || count + 2 > ops.Length) return count;
        for (int i = count - 1; i > k; i--)
        {
            ops[i + 2] = ops[i];
            args[i + 2] = args[i];
        }
        RandomLeaf(ref ops[k + 1], ref args[k + 1], features, constScale);
        ops[k + 2] = (byte)RavineRandom.RangeInt(OpAdd, OpCount);
        args[k + 2] = 0f;
        return count + 2;
    }

    private static int Shrink(Span<byte> ops, Span<float> args, int count, int k)
    {
        if (IsLeaf(ops[k])) return count;
        int start = SubtreeStart(ops, k);
        int keepEnd = k - 1;
        int keepStart = SubtreeStart(ops, keepEnd);
        int removed = (k - start + 1) - (keepEnd - keepStart + 1);
        for (int i = 0; i <= keepEnd - keepStart; i++)
        {
            ops[start + i] = ops[keepStart + i];
            args[start + i] = args[keepStart + i];
        }
        for (int i = k + 1; i < count; i++)
        {
            ops[i - removed] = ops[i];
            args[i - removed] = args[i];
        }
        return count - removed;
    }

    private static void RandomLeaf(ref byte op, ref float arg, int features, float constScale)
    {
        if (features > 0 && RavineRandom.RangeBool())
        {
            op = OpVar;
            arg = RavineRandom.RangeInt(0, features);
        }
        else
        {
            op = OpConst;
            arg = RavineRandom.RangeFloat(-1f, 1f) * constScale;
        }
    }
}
