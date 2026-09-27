using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TheRavine.Generator
{
    public static class FrameBudget
    {
        public const long Unlimited = long.MaxValue;

        public static long Now
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Stopwatch.GetTimestamp();
        }

        public static long TicksFromMs(float ms) => (long)(ms * 0.001 * Stopwatch.Frequency);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Expired(long deadline) => deadline != Unlimited && Stopwatch.GetTimestamp() > deadline;
    }
}
