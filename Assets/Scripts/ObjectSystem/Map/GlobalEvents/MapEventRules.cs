using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "MapEventRules", menuName = "World/Map Event Rules")]
    public sealed class MapEventRules : ScriptableObject
    {
        public FireRules fire = new();

        private void OnValidate()
        {
            fire ??= new FireRules();
            fire.maxSize = Mathf.Max(fire.maxSize, fire.minSize);
            fire.defaultSize = Mathf.Clamp(fire.defaultSize, fire.minSize, fire.maxSize);
            fire.spreadMaxReach = Mathf.Max(fire.spreadMaxReach, 1f);
        }
    }

    [Serializable]
    public sealed class FireRules
    {
        [Range(0f, 1f)] public float ignitionChance = 0.002f;
        public float seasonBias = 0.5f;
        [Min(0.05f)] public float stepInterval = 0.5f;
        [Min(1)] public int maxBurning = 512;

        [Header("Size")]
        [Min(0.01f)] public float defaultSize = 1f;
        [Min(0.01f)] public float minSize = 0.25f;
        [Min(0.01f)] public float maxSize = 8f;

        [Header("Burning")]
        [Min(0.1f)] public float burnSeconds = 6f;
        [Min(0f)] public float burnSizeExponent = 0.5f;
        [Min(0f)] public float damagePerSecond = 8f;
        [Min(0f)] public float heatRadiusPerSize = 0.5f;

        [Header("Spread")]
        [Range(0f, 1f)] public float spreadChance = 0.45f;
        [Min(0f)] public float windBias = 0.8f;
        [Min(0f)] public float spreadReachPerSize = 1.5f;
        [Min(1f)] public float spreadMaxReach = 6f;
        [Min(0f)] public float spreadAttemptsPerSize = 8f;
        [Min(1)] public int spreadMaxAttempts = 32;
    }
}
