using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public sealed class SharedCentroids
    {
        public readonly float[] Centroids;
        public readonly ulong Hash;
        public readonly bool Registered;

        private int _refs;
        private uint[] _lut;
        private float _lutQuant;

        public int RefCount => _refs;
        public bool HasLut => _lut != null;

        internal SharedCentroids(float[] centroids, ulong hash, bool registered)
        {
            Centroids  = centroids;
            Hash       = hash;
            Registered = registered;
        }

        internal void AddRef()
        {
            _refs++;
            if (_refs >= 2 && _lut == null) BuildLut();
        }

        internal bool ReleaseRef()
        {
            if (_refs > 0) _refs--;
            if (_refs > 0) return false;
            _lut = null;
            return true;
        }

        public int Translate(ushort codon, out float d2)
        {
            var lut = _lut;
            if (lut != null)
            {
                uint v = lut[codon];
                d2 = (v >> 8) * _lutQuant;
                return (int)(v & 0xFFu);
            }
            return CodonEmbedding.Nearest(codon, Centroids, VirologyRuntime.CellBias, out d2);
        }

        private unsafe void BuildLut()
        {
            float maxD2 = math.max(SimulationRules.Active.TranslationLutMaxDistanceSq, 1e-3f);
            var lut = new uint[TranslationLutJob.Entries];

            fixed (float* centroids = Centroids)
            fixed (float* bias = VirologyRuntime.CellBias)
            fixed (uint* dst = lut)
            {
                new TranslationLutJob
                {
                    Centroids = centroids,
                    CellBias  = bias,
                    Lut       = dst,
                    Actions   = ProteinTable.ActionCount,
                    ToQuant   = TranslationLutJob.QuantMax / maxD2,
                }.Run(TranslationLutJob.Entries);
            }

            _lutQuant = maxD2 / TranslationLutJob.QuantMax;
            _lut = lut;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public unsafe struct TranslationLutJob : IJobFor
    {
        public const int   Entries  = 1 << 16;
        public const float QuantMax = 0xFFFFFF;

        [NativeDisableUnsafePtrRestriction] public float* Centroids;
        [NativeDisableUnsafePtrRestriction] public float* CellBias;
        [NativeDisableUnsafePtrRestriction] public uint*  Lut;
        public int   Actions;
        public float ToQuant;

        public void Execute(int codon)
        {
            CodonEmbedding.Embed((ushort)codon, out float4 lo, out float4 hi);
            var rows = (float4*)Centroids;

            int   best      = 0;
            float bestScore = float.MaxValue;
            float bestDist  = 0f;

            for (int n = 0; n < Actions; n++)
            {
                float4 a = rows[n << 1] - lo;
                float4 b = rows[(n << 1) + 1] - hi;
                float d2 = math.dot(a, a) + math.dot(b, b);
                float score = d2 - CellBias[n];
                if (score >= bestScore) continue;
                bestScore = score; bestDist = d2; best = n;
            }

            uint q = (uint)math.min(bestDist * ToQuant + 0.5f, QuantMax);
            Lut[codon] = (q << 8) | (uint)best;
        }
    }

    public static class TranslationTableRegistry
    {
        private static readonly Dictionary<ulong, SharedCentroids> Tables = new();

        public static int Count => Tables.Count;

        public static SharedCentroids Acquire(float[] centroids)
        {
            ulong hash = ComputeHash(centroids);
            if (Tables.TryGetValue(hash, out var existing))
            {
                if (SameContent(existing.Centroids, centroids))
                {
                    existing.AddRef();
                    return existing;
                }

                var loose = new SharedCentroids(centroids, hash, false);
                loose.AddRef();
                return loose;
            }

            var shared = new SharedCentroids(centroids, hash, true);
            Tables.Add(hash, shared);
            shared.AddRef();
            return shared;
        }

        public static SharedCentroids Share(SharedCentroids shared)
        {
            shared.AddRef();
            return shared;
        }

        public static void Release(SharedCentroids shared)
        {
            if (shared == null || !shared.ReleaseRef() || !shared.Registered) return;
            if (Tables.TryGetValue(shared.Hash, out var current) && ReferenceEquals(current, shared))
                Tables.Remove(shared.Hash);
        }

        public static unsafe ulong ComputeHash(float[] centroids)
        {
            ulong hash = 14695981039346656037UL;
            fixed (float* p = centroids)
            {
                var bits = (uint*)p;
                for (int i = 0; i < centroids.Length; i++)
                {
                    hash ^= bits[i];
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        private static bool SameContent(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            return new ReadOnlySpan<float>(a).SequenceEqual(new ReadOnlySpan<float>(b));
        }
    }

    public struct TranslationTable
    {
        public SharedCentroids Shared;
        public float Sharpness;

        public const float CentroidSpread = 0.50f;
        public const float JitterScale = 0.18f;
        public const float MutationScale = 0.12f;

        public float[] Centroids => Shared?.Centroids;
        public bool IsCreated => Shared != null;

        public static float[] CreatePrototype(uint seed)
        {
            var array = new float[ProteinTable.ActionCount * ProteinTable.EmbedDim];

            var rng = new XorShift32(seed);
            for (int i = 0; i < array.Length; i++)
                array[i] = NextSymmetric(ref rng) * CentroidSpread;

            return array;
        }

        public static TranslationTable CreateFrom(float[] prototype, float sharpness,
            float jitter, uint seed)
        {
            var centroids = new float[prototype.Length];

            var rng = new XorShift32(seed);
            for (int i = 0; i < prototype.Length; i++)
                centroids[i] = prototype[i] + NextSymmetric(ref rng) * jitter * JitterScale;

            return new TranslationTable
            {
                Sharpness = sharpness,
                Shared    = TranslationTableRegistry.Acquire(centroids),
            };
        }

        public int Translate(ushort codon, out float d2) => Shared.Translate(codon, out d2);

        public static TranslationTable Inherit(in TranslationTable parent, float sharpness, float chance, uint seed)
        {
            var source = parent.Shared.Centroids;
            float[] copy = null;

            var rng = new XorShift32(seed);
            for (int i = 0; i < source.Length; i++)
            {
                if (NextUnit(ref rng) >= chance) continue;

                if (copy == null)
                {
                    copy = new float[source.Length];
                    Array.Copy(source, copy, source.Length);
                }
                copy[i] = math.clamp(copy[i] + NextSymmetric(ref rng) * MutationScale, -1.5f, 1.5f);
            }

            return new TranslationTable
            {
                Sharpness = sharpness,
                Shared    = copy == null
                    ? TranslationTableRegistry.Share(parent.Shared)
                    : TranslationTableRegistry.Acquire(copy),
            };
        }

        public void Release()
        {
            TranslationTableRegistry.Release(Shared);
            Shared = null;
        }

        private static float NextUnit(ref XorShift32 rng)
            => (rng.NextUInt() >> 8) * (1f / 16777216f);

        private static float NextSymmetric(ref XorShift32 rng)
            => NextUnit(ref rng) * 2f - 1f;
    }
}
