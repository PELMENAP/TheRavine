using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class RandomTickMath
    {
        public static float CellsPerTick(float cellsPerChunkPerTick, int ring, float farCellDivisor) =>
            ring <= 0 ? cellsPerChunkPerTick : cellsPerChunkPerTick / (math.max(farCellDivisor, 1f) * ring);

        public static float MeanHitSeconds(int cellsPerChunk, float cellsPerChunkPerTick, float tickSeconds, int ring, float farCellDivisor)
        {
            float perTick = CellsPerTick(cellsPerChunkPerTick, ring, farCellDivisor);
            return perTick > 0f ? cellsPerChunk * tickSeconds / perTick : float.PositiveInfinity;
        }

        public static float NalAcceptChance(byte delay, float delayReference) =>
            math.clamp(delayReference / math.max((int)delay, 1), 0f, 1f);

        public static float DecayChance(float meanHitSeconds, float decaySeconds) =>
            decaySeconds > 0f ? 1f - math.exp(-meanHitSeconds / decaySeconds) : 1f;
    }
}
