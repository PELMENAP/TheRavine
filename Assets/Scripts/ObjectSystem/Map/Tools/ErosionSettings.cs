using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public struct ErosionSettings
    {
        public bool enabled;

        // Количество капель на чанк. 3000–8000 — баланс качество/время для 40x40.
        [Range(100, 50000)] public int dropletCount;
        [Range(10, 128)] public int lifetime;
        [Range(0.001f, 5f)] public float startSpeed;
        [Range(0.01f, 4f)] public float acceleration;
        [Range(0f, 0.99f)] public float drag;
        [Range(0.1f, 2f)] public float startWater;
        [Range(0.1f, 20f)] public float sedimentCapacityFactor;
        [Range(0f, 1f)] public float depositSpeed;
        [Range(0f, 1f)] public float erodeSpeed;
        [Range(0f, 30f)] public float gravity;
        [Range(0f, 0.5f)] public float evaporateSpeed;
        public int SafeLifetime => Mathf.Clamp(lifetime, 1, 128);

        public static ErosionSettings Default => new()
        {
            enabled               = true,
            dropletCount          = 4000,
            lifetime              = 80,
            startSpeed            = 1f,
            acceleration          = 1f,
            drag                  = 0.08f,
            startWater            = 1f,
            sedimentCapacityFactor = 4f,
            depositSpeed          = 0.3f,
            erodeSpeed            = 0.3f,
            gravity               = 4f,
            evaporateSpeed        = 0.04f,
        };
        public static ErosionSettings Mountain => new()
        {
            enabled               = true,
            dropletCount          = 6000,
            lifetime              = 120,
            startSpeed            = 2f,
            acceleration          = 1.5f,
            drag                  = 0.05f,
            startWater            = 1f,
            sedimentCapacityFactor = 8f,
            depositSpeed          = 0.2f,
            erodeSpeed            = 0.5f,
            gravity               = 8f,
            evaporateSpeed        = 0.02f,
        };
        public static ErosionSettings Plains => new()
        {
            enabled               = true,
            dropletCount          = 2000,
            lifetime              = 50,
            startSpeed            = 0.5f,
            acceleration          = 0.5f,
            drag                  = 0.15f,
            startWater            = 1f,
            sedimentCapacityFactor = 2f,
            depositSpeed          = 0.5f,
            erodeSpeed            = 0.15f,
            gravity               = 2f,
            evaporateSpeed        = 0.06f,
        };
    }
}