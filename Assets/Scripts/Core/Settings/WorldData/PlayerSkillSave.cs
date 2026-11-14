using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class PlayerSkillSave
    {
        public int skillCount;
        public float[] genes;
        public float[] forest;
        public float[] levels;
        public float[] peaks;
        public ulong[] active;
        public ulong islandMask;
        public ulong prunedMask;
        public ushort[] bridges;
        public float phiPeak;
        public byte spentEdgePoints;
        public bool autoMode;

        public PlayerSkillSave Clone() => new()
        {
            skillCount = skillCount,
            genes = genes != null ? (float[])genes.Clone() : null,
            forest = forest != null ? (float[])forest.Clone() : null,
            levels = levels != null ? (float[])levels.Clone() : null,
            peaks = peaks != null ? (float[])peaks.Clone() : null,
            active = active != null ? (ulong[])active.Clone() : null,
            islandMask = islandMask,
            prunedMask = prunedMask,
            bridges = bridges != null ? (ushort[])bridges.Clone() : null,
            phiPeak = phiPeak,
            spentEdgePoints = spentEdgePoints,
            autoMode = autoMode,
        };
    }
}
