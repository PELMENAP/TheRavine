using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct LawRecord
{
    public const int Capacity = 16;

    public float D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, D10, D11, D12, D13, D14, D15;
    public int Id;
    public int Author;
    public float Support;
    public float Created;
    public byte Alive;

    public readonly bool IsAlive => Alive != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<float> Delta(ref LawRecord law)
        => MemoryMarshal.CreateSpan(ref Unsafe.As<LawRecord, float>(ref law), Capacity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<float> DeltaReadOnly(in LawRecord law)
        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<LawRecord, float>(ref Unsafe.AsRef(in law)), Capacity);
}
