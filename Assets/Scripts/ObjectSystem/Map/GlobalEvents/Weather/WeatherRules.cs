using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class WeatherRules
    {
        [Header("Step")]
        [Min(0.05f)] public float stepInterval = 0.5f;
        [Min(1)] public int maxCells = 16;

        [Header("Map")]
        [Min(8)] public int mapSide = 64;
        [Min(1f)] public float texelSize = 56f;

        [Header("Spawn")]
        [Min(0f)] public float convectivePerHour = 90f;
        [Min(0f)] public float stratiformPerHour = 24f;
        [Range(0f, 1f)] public float heatPeakPhase = 0.82f;
        [Range(0.02f, 0.5f)] public float heatWidth = 0.14f;
        public float heatSeasonBias = 0.35f;
        public float spawnSeasonBias = 0.5f;
        [Range(0f, 1f)] public float winterPrecipitation = 0.5f;
        [Range(0f, 1f)] public float upwindShare = 0.6f;
        [Min(0f)] public float spawnMargin = 128f;

        [Header("Cells")]
        [Min(1f)] public float radiusMin = 140f;
        [Min(1f)] public float radiusMax = 420f;
        [Min(1f)] public float lifeMin = 45f;
        [Min(1f)] public float lifeMax = 240f;
        [Range(0f, 1f)] public float lifeMoistureWeight = 0.7f;
        [Min(0f)] public float driftFactor = 40f;
        [Min(0.1f)] public float inertiaSeconds = 20f;
        [Range(0f, 1f)] public float moistureMin = 0.1f;
        [Range(0f, 1f)] public float moistureMax = 1f;
        [Range(0f, 1f)] public float chargeMin = 0.2f;
        [Range(0f, 1f)] public float chargeMax = 1f;
        [Range(0f, 1f)] public float chargeHeatCorrelation = 0.8f;
        [Min(1f)] public float cloudSpread = 1.4f;
        [Range(0f, 1f)] public float cloudBase = 0.4f;

        [Header("Cloud layer")]
        [Tooltip("Cloud base elevation in world coordinates. Used by clouds, rain shafts and lightning.")]
        public float cloudPlaneY = 650f;
        [Range(0f, 1f)] public float fairWeatherCover = 0.36f;

        [Header("Ground")]
        [Min(0f)] public float virgaStrength = 0.8f;
        [Min(0f)] public float wetnessGain = 0.04f;
        [Min(0f)] public float wetnessDryPerSecond = 0.004f;
        [Min(0f)] public float wetnessDryHeat = 1.5f;
        [Min(0f)] public float wetnessDryWind = 2f;

        [Header("Pressure and wind")]
        [Min(0f)] public float pressurePerMoisture = 6f;
        [Min(0f)] public float pressurePerCharge = 10f;
        [Min(0f)] public float gustStrength = 0.6f;

        [Header("Lightning")]
        [Min(0f)] public float strikesPerSecond = 0.5f;
        [Range(0f, 1f)] public float strikeThreshold = 0.25f;
        [Range(0f, 1f)] public float groundThreshold = 0.5f;
        [Min(1)] public int candidateSamples = 8;
        [Range(0.1f, 1f)] public float strikeSpread = 0.8f;
        [Min(1f)] public float strikeRadius = 24f;
        [Range(0f, 1f)] public float ignitionPerStrike = 0.6f;
        [Min(0f)] public float strikeDamage = 60f;
        [Min(0.1f)] public float creatureStrikeRadius = 3f;
        [Min(1)] public int strikeHistory = 64;

        public void Validate()
        {
            stepInterval = Mathf.Max(stepInterval, 0.05f);
            maxCells = Mathf.Max(maxCells, 1);
            mapSide = Mathf.Max(mapSide, 8);
            texelSize = Mathf.Max(texelSize, 1f);
            heatWidth = Mathf.Clamp(heatWidth, 0.02f, 0.5f);
            radiusMin = Mathf.Max(radiusMin, 1f);
            radiusMax = Mathf.Max(radiusMax, radiusMin);
            lifeMin = Mathf.Max(lifeMin, 1f);
            lifeMax = Mathf.Max(lifeMax, lifeMin);
            inertiaSeconds = Mathf.Max(inertiaSeconds, 0.1f);
            moistureMax = Mathf.Max(moistureMax, moistureMin);
            chargeMax = Mathf.Max(chargeMax, chargeMin);
            cloudSpread = Mathf.Max(cloudSpread, 1f);
            groundThreshold = Mathf.Max(groundThreshold, strikeThreshold);
            candidateSamples = Mathf.Max(candidateSamples, 1);
            strikeHistory = Mathf.Max(strikeHistory, 1);
        }
    }
}
