using UnityEngine;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "WorldTickRules", menuName = "World/World Tick Rules")]
    public sealed class WorldTickRules : ScriptableObject
    {
        [Min(0.01f)] public float tickSeconds = 1f;
        [Min(0f)] public float cellsPerChunkPerTick = 16f;
        [Min(1f)] public float farCellDivisor = 8f;
        [Min(1)] public int maxTicksPerFrame = 4;
        [Min(1)] public int maxEventsPerTick = 1024;
        [Min(1)] public int regionRebuildIntervalFrames = 30;
        [Min(0f)] public float warmupSeconds = 10f;
        [Min(0f)] public float catchUpMaxSeconds = 600f;
        [Min(0)] public int catchUpBurstRounds = 4;
        [Min(1f)] public float nalDelayReference = 10f;
        [Range(0f, 1f)] public float growAcceptChance = 1f;
    }
}
