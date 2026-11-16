using System;
using UnityEngine;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class WildWindRules
    {
        [Header("Band")]
        [Min(0f)] public float bandBottom = 3000f;
        [Min(0f)] public float bandTop = 4000f;
        [Min(0f)] public float fadeWidth = 150f;

        [Header("Gusts")]
        [Min(0.02f)] public float stepInterval = 0.25f;
        [Min(1)] public int gustCount = 24;
        [Min(0.1f)] public float gustRadius = 40f;
        [Min(0f)] public float gustSpeed = 30f;
        [Min(0f)] public float driftScale = 1f;
        [Min(0f)] public float lostTargetSeconds = 3f;
        [Min(1)] public int reportCapacity = 256;

        [Header("Harm")]
        [Min(0f)] public float damagePerSecond = 15f;

        public void Validate()
        {
            bandBottom = Mathf.Max(bandBottom, 0f);
            bandTop = Mathf.Max(bandTop, bandBottom);
            fadeWidth = Mathf.Clamp(fadeWidth, 0f, (bandTop - bandBottom) * 0.5f);
            stepInterval = Mathf.Max(stepInterval, 0.02f);
            gustCount = Mathf.Max(gustCount, 1);
            gustRadius = Mathf.Max(gustRadius, 0.1f);
            reportCapacity = Mathf.Max(reportCapacity, 1);
        }
    }
}
