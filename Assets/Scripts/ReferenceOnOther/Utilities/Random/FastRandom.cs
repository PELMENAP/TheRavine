using UnityEngine;

namespace TheRavine.Extensions
{
    public sealed class FastRandom : IRandom
    {
        public int Seed { get; private set; }
        private const ulong Modulus = 2147483647; //2^31
        private const ulong Multiplier = 1132489760;
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
        public float GetFloat()
        {
            return (float)InternalSample();
        }
        public int GetInt()
        {
            return Range(int.MinValue, int.MaxValue);
        }
        public float Range(float min, float max)
        {
            return (float)(InternalSample() * (max - min) + min);
        }
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
            return GetRotationOnSurface(GetInsideSphere());
        }

        public Quaternion GetRotationOnSurface(Vector3 surface)
        {
            return new Quaternion(surface.x, surface.y, surface.z, GetFloat());
        }

        public Color GetColor()
        {
            return new Color(Range(0f, 1f), Range(0f, 1f), Range(0f, 1f), Range(0.8f, 1f));
        }

        private double InternalSample()
        {
            var ret = _next * ModulusReciprocal;
            _next = _next * Multiplier % Modulus;
            return ret;
        }
    }

    public static class RavineRandom
    {
        private static FastRandom fastRandom = new FastRandom();
        public static int RangeInt(int min, int max) => fastRandom.Range(min, max);
        public static float RangeFloat(float min, float max) => fastRandom.Range(min, max);
        public static int Hundred() => RangeInt(0, 100);
        public static Vector2 GetInsideCircle(float radius = 1) => fastRandom.GetInsideCircle(radius);
        public static Color RangeColor() => fastRandom.GetColor();
        // public static Vector2 GetInsideCircleSquare(float radius) => fastRandom.GetInsideCircle(radius);
    }
}
