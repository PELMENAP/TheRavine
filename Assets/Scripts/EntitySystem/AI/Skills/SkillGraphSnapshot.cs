using UnityEngine;

public enum SkillVertexState : byte
{
    Hidden = 0,
    Open,
    Available,
    Silhouette,
    Island,
    Pruned,
}

public enum SkillEdgeKind : byte
{
    Active = 0,
    Bridge,
    Potential,
}

public sealed class SkillGraphSnapshot
{
    public const int MaxVertices = SkillAtlas.MaxCount;
    public const int MaxEdges = SkillAtlas.MaxCount * 4;

    public int VertexCount;
    public int EdgeCount;
    public int Revision;
    public int StateVersion;
    public int LevelChecksum;
    public bool AutoMode;
    public int Earned;
    public int Spent;
    public int Available;
    public float PhiPeak;
    public float FocusBoost;

    public readonly int[] Skill = new int[MaxVertices];
    public readonly Vector2[] Position = new Vector2[MaxVertices];
    public readonly int[] Level = new int[MaxVertices];
    public readonly float[] Fraction = new float[MaxVertices];
    public readonly float[] Peak = new float[MaxVertices];
    public readonly float[] Floor = new float[MaxVertices];
    public readonly float[] Talent = new float[MaxVertices];
    public readonly SkillVertexState[] State = new SkillVertexState[MaxVertices];
    public readonly byte[] Clique = new byte[MaxVertices];
    public readonly float[] Decay = new float[MaxVertices];
    public readonly float[] CliqueBoost = new float[MaxVertices];
    public readonly float[] LogicBoost = new float[MaxVertices];

    public readonly int[] EdgeFrom = new int[MaxEdges];
    public readonly int[] EdgeTo = new int[MaxEdges];
    public readonly SkillEdgeKind[] EdgeKind = new SkillEdgeKind[MaxEdges];

    private readonly int[] _slotOfSkill = new int[MaxVertices];

    public void Clear()
    {
        VertexCount = 0;
        EdgeCount = 0;
        for (int i = 0; i < _slotOfSkill.Length; i++) _slotOfSkill[i] = -1;
    }

    public int SlotOf(int skill) => (uint)skill < (uint)_slotOfSkill.Length ? _slotOfSkill[skill] : -1;

    public int AddVertex(int skill)
    {
        int slot = VertexCount++;
        Skill[slot] = skill;
        _slotOfSkill[skill] = slot;
        return slot;
    }

    public bool AddEdge(int from, int to, SkillEdgeKind kind)
    {
        if (EdgeCount >= MaxEdges) return false;
        EdgeFrom[EdgeCount] = from;
        EdgeTo[EdgeCount] = to;
        EdgeKind[EdgeCount] = kind;
        EdgeCount++;
        return true;
    }
}
