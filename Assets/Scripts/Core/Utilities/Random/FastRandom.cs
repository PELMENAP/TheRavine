using UnityEngine;
using System.Runtime.CompilerServices;

namespace TheRavine.Extensions
{
    public sealed class FastRandom
    {
        public int Seed { get; private set; }

        private const ulong Modulus = 2147483647UL;
        private const ulong Multiplier = 1132489760UL;
        private const double ModulusReciprocal = 1.0 / Modulus;
        private ulong _next;

        public FastRandom() : this(RandomSeed.Crypto()) { }
        public FastRandom(int seed)
        {
            NewSeed(seed);
        }
        public void NewSeed()
        {
            NewSeed(RandomSeed.Crypto());
        }
        public void NewSeed(int seed)
        {
            if (seed == 0)
                seed = 1;
            Seed = seed;
            _next = (ulong)seed % Modulus;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFloat()
        {
            return (float)InternalSample();
        }
        public int GetInt()
        {
            return Range(int.MinValue, int.MaxValue);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Range(float min, float max)
        {
            return (float)(InternalSample() * (max - min) + min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Next(int max) => Range(0, max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Range(int min, int max)
        {
            return (int)(InternalSample() * (max - min) + min);
        }
        public Vector2 GetInsideCircle(float radius = 1)
        {
            var x = Range(-1f, 1f) * radius;
            var y = Range(-1f, 1f) * radius;
            return new Vector2(x, y);
        }

        public Vector3 GetInsideSphere(float radius = 1)
        {
            var x = Range(-1f, 1f) * radius;
            var y = Range(-1f, 1f) * radius;
            var z = Range(-1f, 1f) * radius;
            return new Vector3(x, y, z);
        }

        public Unity.Mathematics.float2 Get2Direction(float radius = 1)
        {
            var x = Range(-1f, 1f) * radius;
            var y = Range(-1f, 1f) * radius;
            return new Unity.Mathematics.float2(x, y);
        }
        public Quaternion GetRotation()
        {
            float u1 = Range(0f, 1f);
            float u2 = Range(0f, Mathf.PI * 2f);
            float u3 = Range(0f, Mathf.PI * 2f);
            float sq1 = Mathf.Sqrt(1f - u1), sq2 = Mathf.Sqrt(u1);
            return new Quaternion(
                sq1 * Mathf.Sin(u2), sq1 * Mathf.Cos(u2),
                sq2 * Mathf.Sin(u3), sq2 * Mathf.Cos(u3));
        }
        public Color GetColor()
        {
            return new Color(Range(0f, 1f), Range(0f, 1f), Range(0f, 1f), Range(0.8f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MersenneMod(ulong value)
        {
            value = (value >> 31) + (value & Modulus);
            if (value >= Modulus) value -= Modulus;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double InternalSample()
        {
            var ret = _next * ModulusReciprocal;
            _next = MersenneMod(_next * Multiplier);
            return ret;
        }
    }

    public static class RavineRandom
    {
        private readonly static FastRandom fastRandom = new();
        public static int RangeInt(int min, int max) => fastRandom.Range(min, max);
        public static int RangeInt(int max) => fastRandom.Range(0, max);
        public static float RangeFloat(float min, float max) => fastRandom.Range(min, max);
        public static float RangeFloat() => fastRandom.Range(0f, 1f);
        public static int Hundred() => RangeInt(0, 100);
        public static Vector2 GetInsideCircle(float radius = 1) => fastRandom.GetInsideCircle(radius);
        public static Color RangeColor() => fastRandom.GetColor();
        public static bool RangeBool() => (fastRandom.GetInt() & 1) == 0;
    }
}
