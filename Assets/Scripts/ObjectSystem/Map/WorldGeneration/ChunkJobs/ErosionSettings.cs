using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public struct ErosionSettings
    {
        public bool allowInfiniteErosionDepth;

        [Range(100, 50000)]
        public int dropletCount;

        [Range(4, 256)]
        public int lifetime;

        [Range(0.1f, 10f)]
        public float amplify;

        [Range(0.001f, 1f)]
        public float inertia;

        [Range(0.001f, 20f)]
        public float gravity;

        [Range(0.001f, 1f)]
        public float evaporation;

        [Range(0.001f, 32f)]
        public float sedimentCapacity;

        [Range(0f, 1f)]
        public float depositSpeed;

        [Range(0f, 1f)]
        public float erodeSpeed;

        [Range(0.00001f, 1f)]
        public float minSlope;

        [Range(1, 8)]
        public int radius;
    }

}