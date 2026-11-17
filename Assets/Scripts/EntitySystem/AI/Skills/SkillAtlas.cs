using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillAtlas", menuName = "Simulation/Skill Atlas")]
public sealed class SkillAtlas : ScriptableObject
{
    public const int MinCount = 16;
    public const int MaxCount = 64;
    public const int MaxXpPerAction = 2;

    [Serializable]
    public struct Skill
    {
        public string Name;
        public SkillChannel Channel;
        public bool IsDivider;
        public SkillCurve Curve;
        public float BaseXp;
        public float XpExp;
        public SkillNeed Need;
    }

    [Serializable]
    public struct XpSource
    {
        public byte Action;
        public byte Skill;
        public float Xp;
    }

    [SerializeField] private int count;
    [SerializeField] private Vector2[] positions = Array.Empty<Vector2>();
    [SerializeField] private ulong[] potential = Array.Empty<ulong>();
    [SerializeField] private float meanEdgeLength;
    [SerializeField] private Skill[] skills = Array.Empty<Skill>();
    [SerializeField] private XpSource[] xpSources = Array.Empty<XpSource>();

    private int[] _xpStart;
    private NativeArray<float2> _nativePositions;
    private NativeArray<ulong> _nativePotential;
    private NativeArray<float3> _nativeCurves;
    private NativeArray<float2> _nativeXpCost;
    private NativeArray<byte> _nativeChannels;
    private NativeArray<byte> _nativeDividers;

    private static SkillAtlas _active;
    private static SkillAtlas _default;

    public int Count => count;
    public float MeanEdgeLength => meanEdgeLength;
    public int XpSourceCount => xpSources.Length;

    public static SkillAtlas Active
    {
        get
        {
            if (_active != null) return _active;
            if (_default == null) _default = SkillAtlasBuilder.BuildDefault();
            return _default;
        }
    }

    public static void Bind(SkillAtlas atlas)
    {
        if (atlas != null && !atlas.Validate(out string error))
            throw new InvalidOperationException("SkillAtlas is invalid: " + error);
        _active = atlas;
    }

    public static SkillAtlas Create(Vector2[] positions, ulong[] potential, Skill[] skills, XpSource[] xpSources)
    {
        var atlas = CreateInstance<SkillAtlas>();
        atlas.count = positions.Length;
        atlas.positions = positions;
        atlas.potential = potential;
        atlas.skills = skills;
        atlas.xpSources = SortedByAction(xpSources);
        atlas.meanEdgeLength = ComputeMeanEdgeLength(positions, potential);
        atlas.BuildIndex();
        return atlas;
    }

    public Vector2 PositionOf(int skill) => positions[skill];
    public ulong PotentialRow(int skill) => potential[skill];
    public ref readonly Skill SkillAt(int skill) => ref skills[skill];
    public ref readonly XpSource XpSourceAt(int index) => ref xpSources[index];

    public bool HasPotentialEdge(int u, int v)
        => (uint)u < (uint)count && (uint)v < (uint)count && u != v && ((potential[u] >> v) & 1ul) != 0;

    public bool TryGetXpRange(int action, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (_xpStart == null) BuildIndex();
        if ((uint)action >= (uint)(_xpStart.Length - 1)) return false;
        start = _xpStart[action];
        length = _xpStart[action + 1] - start;
        return length > 0;
    }

    public NativeArray<float2> NativePositions { get { EnsureNative(); return _nativePositions; } }
    public NativeArray<ulong> NativePotential { get { EnsureNative(); return _nativePotential; } }
    public NativeArray<float3> NativeCurves { get { EnsureNative(); return _nativeCurves; } }
    public NativeArray<float2> NativeXpCost { get { EnsureNative(); return _nativeXpCost; } }
    public NativeArray<byte> NativeChannels { get { EnsureNative(); return _nativeChannels; } }
    public NativeArray<byte> NativeDividers { get { EnsureNative(); return _nativeDividers; } }

    public void EnsureNative()
    {
        if (_nativePositions.IsCreated) return;
        _nativePositions = new NativeArray<float2>(count, Allocator.Persistent);
        _nativePotential = new NativeArray<ulong>(count, Allocator.Persistent);
        _nativeCurves = new NativeArray<float3>(count, Allocator.Persistent);
        _nativeXpCost = new NativeArray<float2>(count, Allocator.Persistent);
        _nativeChannels = new NativeArray<byte>(count, Allocator.Persistent);
        _nativeDividers = new NativeArray<byte>(count, Allocator.Persistent);
        for (int i = 0; i < count; i++)
        {
            _nativePositions[i] = positions[i];
            _nativePotential[i] = potential[i];
            ref readonly var s = ref skills[i];
            _nativeCurves[i] = new float3(s.Curve.P, s.Curve.L0, s.Curve.C);
            _nativeXpCost[i] = new float2(s.BaseXp, s.XpExp);
            _nativeChannels[i] = (byte)s.Channel;
            _nativeDividers[i] = s.IsDivider ? (byte)1 : (byte)0;
        }
    }

    public void ReleaseNative()
    {
        if (_nativePositions.IsCreated) _nativePositions.Dispose();
        if (_nativePotential.IsCreated) _nativePotential.Dispose();
        if (_nativeCurves.IsCreated) _nativeCurves.Dispose();
        if (_nativeXpCost.IsCreated) _nativeXpCost.Dispose();
        if (_nativeChannels.IsCreated) _nativeChannels.Dispose();
        if (_nativeDividers.IsCreated) _nativeDividers.Dispose();
    }

    private void OnEnable() => BuildIndex();

    private void OnDisable() => ReleaseNative();

    private void OnValidate()
    {
        if (Validate(out _)) meanEdgeLength = ComputeMeanEdgeLength(positions, potential);
        BuildIndex();
        ReleaseNative();
    }

    private void BuildIndex()
    {
        int actions = ActionCatalog.Count;
        var start = new int[actions + 1];
        for (int i = 0; i < xpSources.Length; i++)
            if (xpSources[i].Action < actions) start[xpSources[i].Action + 1]++;
        for (int a = 0; a < actions; a++) start[a + 1] += start[a];
        _xpStart = start;
    }

    public bool Validate(out string error)
    {
        error = null;
        if (count < MinCount || count > MaxCount) { error = "count " + count + " outside " + MinCount + ".." + MaxCount; return false; }
        if (positions == null || positions.Length != count) { error = "positions length"; return false; }
        if (potential == null || potential.Length != count) { error = "potential length"; return false; }
        if (skills == null || skills.Length != count) { error = "skills length"; return false; }
        ulong all = count == 64 ? ulong.MaxValue : (1ul << count) - 1ul;
        for (int i = 0; i < count; i++)
        {
            var p = positions[i];
            if (!(p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f)) { error = "position " + i + " outside the unit square"; return false; }
            if ((potential[i] & ~all) != 0ul) { error = "row " + i + " has bits beyond count"; return false; }
            if (((potential[i] >> i) & 1ul) != 0ul) { error = "self loop at " + i; return false; }
            for (int j = i + 1; j < count; j++)
                if (((potential[i] >> j) & 1ul) != ((potential[j] >> i) & 1ul)) { error = "asymmetric edge " + i + "-" + j; return false; }
            ref readonly var s = ref skills[i];
            if (!(s.BaseXp > 0f) || !(s.XpExp >= 0f)) { error = "xp cost of skill " + i; return false; }
            if (!(s.Curve.P > 0f) || !(s.Curve.L0 > 0f) || !(s.Curve.C >= 0f)) { error = "curve of skill " + i; return false; }
            if (s.Channel >= SkillChannel.Count || s.Need >= SkillNeed.Count) { error = "enum of skill " + i; return false; }
        }
        if (SkillAtlasBuilder.HasK5(potential, count)) { error = "K5 in the potential frame"; return false; }
        if (xpSources != null)
        {
            var perAction = new int[ActionCatalog.Count];
            for (int i = 0; i < xpSources.Length; i++)
            {
                var x = xpSources[i];
                if (x.Action >= ActionCatalog.Count || x.Skill >= count || !(x.Xp >= 0f) || ++perAction[x.Action] > MaxXpPerAction)
                { error = "xp source " + i; return false; }
            }
        }
        return true;
    }

    private static XpSource[] SortedByAction(XpSource[] source)
    {
        var sorted = (XpSource[])source.Clone();
        Array.Sort(sorted, (a, b) => a.Action.CompareTo(b.Action));
        return sorted;
    }

    private static float ComputeMeanEdgeLength(Vector2[] points, ulong[] rows)
    {
        float sum = 0f;
        int edges = 0;
        for (int u = 0; u < points.Length; u++)
        for (int v = u + 1; v < points.Length; v++)
        {
            if (((rows[u] >> v) & 1ul) == 0ul) continue;
            sum += Vector2.Distance(points[u], points[v]);
            edges++;
        }
        return edges > 0 ? sum / edges : 0f;
    }

    public static SkillAtlas BuildDefault() => SkillAtlasBuilder.BuildDefault();
}
