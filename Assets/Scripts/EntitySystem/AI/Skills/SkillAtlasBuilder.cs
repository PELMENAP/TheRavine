using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public static class SkillAtlasBuilder
{
    public const uint DefaultSeed = 0x5A17C0DEu;
    public const int DefaultCount = 18;
    public const float PoissonRadiusScale = 0.83f;
    public const float PointMargin = 0.03f;
    public const int OversampleFactor = 40;
    public const float RadiusRelax = 0.9f;
    private const double Eps = 1e-12;

    private struct Tri
    {
        public int A, B, C;
        public double Ox, Oy, R2;
    }

    private struct Quad
    {
        public int T1, T2, B, D;
        public double Measure;
    }

    public static SkillAtlas BuildDefault(int count = DefaultCount, uint seed = DefaultSeed)
    {
        float quantile = SimulationRules.IsBound ? SimulationRules.Active.SkillK4Quantile : SimulationRules.DefaultSkillK4Quantile;
        Generate(count, seed, quantile, out var points, out var potential);
        var positions = new Vector2[count];
        for (int i = 0; i < count; i++) positions[i] = new Vector2(points[i].x, points[i].y);
        var skills = DefaultSkills();
        var table = DefaultXpSources();
        if (skills.Length != count)
        {
            var resized = new SkillAtlas.Skill[count];
            for (int i = 0; i < count; i++) resized[i] = i < skills.Length ? skills[i] : FillerSkill(i);
            skills = resized;
        }
        return SkillAtlas.Create(positions, potential, skills, table);
    }

    public static void Generate(int count, uint seed, float k4Quantile, out float2[] points, out ulong[] potential)
    {
        var rng = Random.CreateFromIndex(seed);
        points = SamplePoints(count, ref rng);
        potential = new ulong[count];
        var tris = Delaunay(points);
        for (int t = 0; t < tris.Count; t++)
        {
            Link(potential, tris[t].A, tris[t].B);
            Link(potential, tris[t].B, tris[t].C);
            Link(potential, tris[t].C, tris[t].A);
        }
        AddSecondDiagonals(points, potential, tris, k4Quantile);
    }

    public static bool HasK5(ulong[] potential, int count)
    {
        for (int a = 0; a < count; a++)
        {
            ulong na = potential[a];
            for (ulong rb = na & ~((2ul << a) - 1ul); rb != 0ul; rb &= rb - 1ul)
            {
                int b = math.tzcnt(rb);
                ulong nab = na & potential[b];
                for (ulong rc = nab & ~((2ul << b) - 1ul); rc != 0ul; rc &= rc - 1ul)
                {
                    int c = math.tzcnt(rc);
                    ulong nabc = nab & potential[c];
                    for (ulong rd = nabc & ~((2ul << c) - 1ul); rd != 0ul; rd &= rd - 1ul)
                    {
                        int d = math.tzcnt(rd);
                        if ((nabc & potential[d]) != 0ul) return true;
                    }
                }
            }
        }
        return false;
    }

    private static void Link(ulong[] rows, int a, int b)
    {
        rows[a] |= 1ul << b;
        rows[b] |= 1ul << a;
    }

    private static float2[] SamplePoints(int count, ref Random rng)
    {
        float radius = PoissonRadiusScale / math.sqrt(count);
        var pool = new List<float2>(count * 2);
        while (true)
        {
            pool.Clear();
            int tries = count * OversampleFactor;
            for (int i = 0; i < tries; i++)
            {
                float2 p = PointMargin + rng.NextFloat2() * (1f - 2f * PointMargin);
                bool free = true;
                for (int j = 0; j < pool.Count && free; j++) free = math.distancesq(pool[j], p) >= radius * radius;
                if (free) pool.Add(p);
            }
            if (pool.Count >= count) break;
            radius *= RadiusRelax;
        }
        while (pool.Count > count)
        {
            int drop = rng.NextInt(pool.Count);
            pool[drop] = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        return pool.ToArray();
    }

    private static List<Tri> Delaunay(float2[] points)
    {
        int n = points.Length;
        var x = new double[n + 3];
        var y = new double[n + 3];
        for (int i = 0; i < n; i++) { x[i] = points[i].x; y[i] = points[i].y; }
        x[n] = -50; y[n] = -50;
        x[n + 1] = 50; y[n + 1] = -50;
        x[n + 2] = 0.5; y[n + 2] = 100;

        var tris = new List<Tri> { MakeTri(x, y, n, n + 1, n + 2) };
        var bad = new List<int>();
        var edges = new List<(int, int)>();
        for (int p = 0; p < n; p++)
        {
            bad.Clear();
            for (int t = 0; t < tris.Count; t++)
            {
                double dx = x[p] - tris[t].Ox, dy = y[p] - tris[t].Oy;
                if (dx * dx + dy * dy < tris[t].R2 - Eps) bad.Add(t);
            }
            edges.Clear();
            for (int k = 0; k < bad.Count; k++)
            {
                var t = tris[bad[k]];
                edges.Add((t.A, t.B));
                edges.Add((t.B, t.C));
                edges.Add((t.C, t.A));
            }
            for (int k = bad.Count - 1; k >= 0; k--)
            {
                tris[bad[k]] = tris[tris.Count - 1];
                tris.RemoveAt(tris.Count - 1);
            }
            for (int e = 0; e < edges.Count; e++)
            {
                int shared = 0;
                for (int f = 0; f < edges.Count; f++)
                    if (e != f && ((edges[e].Item1 == edges[f].Item1 && edges[e].Item2 == edges[f].Item2)
                        || (edges[e].Item1 == edges[f].Item2 && edges[e].Item2 == edges[f].Item1))) shared++;
                if (shared == 0) tris.Add(MakeTri(x, y, edges[e].Item1, edges[e].Item2, p));
            }
        }
        for (int t = tris.Count - 1; t >= 0; t--)
            if (tris[t].A >= n || tris[t].B >= n || tris[t].C >= n) tris.RemoveAt(t);
        return tris;
    }

    private static Tri MakeTri(double[] x, double[] y, int a, int b, int c)
    {
        double d = 2.0 * (x[a] * (y[b] - y[c]) + x[b] * (y[c] - y[a]) + x[c] * (y[a] - y[b]));
        double a2 = x[a] * x[a] + y[a] * y[a], b2 = x[b] * x[b] + y[b] * y[b], c2 = x[c] * x[c] + y[c] * y[c];
        double ox = (a2 * (y[b] - y[c]) + b2 * (y[c] - y[a]) + c2 * (y[a] - y[b])) / d;
        double oy = (a2 * (x[c] - x[b]) + b2 * (x[a] - x[c]) + c2 * (x[b] - x[a])) / d;
        double dx = x[a] - ox, dy = y[a] - oy;
        return new Tri { A = a, B = b, C = c, Ox = ox, Oy = oy, R2 = dx * dx + dy * dy };
    }

    private static double Orient(float2 a, float2 b, float2 c)
        => (double)(b.x - a.x) * (c.y - a.y) - (double)(b.y - a.y) * (c.x - a.x);

    private static void AddSecondDiagonals(float2[] points, ulong[] potential, List<Tri> tris, float quantile)
    {
        var edgeTris = new Dictionary<(int, int), List<int>>();
        for (int t = 0; t < tris.Count; t++)
        {
            Register(edgeTris, tris[t].A, tris[t].B, t);
            Register(edgeTris, tris[t].B, tris[t].C, t);
            Register(edgeTris, tris[t].C, tris[t].A, t);
        }

        var quads = new List<Quad>();
        foreach (var pair in edgeTris)
        {
            if (pair.Value.Count != 2) continue;
            int a = pair.Key.Item1, c = pair.Key.Item2;
            int t1 = pair.Value[0], t2 = pair.Value[1];
            int b = Third(tris[t1], a, c), d = Third(tris[t2], a, c);
            float2 pa = points[a], pb = points[b], pc = points[c], pd = points[d];
            bool convex = Orient(pb, pd, pa) * Orient(pb, pd, pc) < 0.0 && Orient(pa, pc, pb) * Orient(pa, pc, pd) < 0.0;
            if (!convex) continue;
            var t = tris[t1];
            double dx = pd.x - t.Ox, dy = pd.y - t.Oy;
            double measure = math.abs(dx * dx + dy * dy - t.R2) / t.R2;
            quads.Add(new Quad { T1 = t1, T2 = t2, B = b, D = d, Measure = measure });
        }
        quads.Sort((l, r) => l.Measure != r.Measure ? l.Measure.CompareTo(r.Measure) : (l.T1 != r.T1 ? l.T1.CompareTo(r.T1) : l.T2.CompareTo(r.T2)));

        int take = math.clamp((int)math.round(quantile * quads.Count), 0, quads.Count);
        var used = new bool[tris.Count];
        for (int i = 0; i < take; i++)
        {
            var q = quads[i];
            if (used[q.T1] || used[q.T2]) continue;
            Link(potential, q.B, q.D);
            if (HasK5(potential, potential.Length))
            {
                potential[q.B] &= ~(1ul << q.D);
                potential[q.D] &= ~(1ul << q.B);
                continue;
            }
            used[q.T1] = true;
            used[q.T2] = true;
        }
    }

    private static void Register(Dictionary<(int, int), List<int>> map, int a, int b, int tri)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<int>(2);
        list.Add(tri);
    }

    private static int Third(Tri t, int a, int c)
        => t.A != a && t.A != c ? t.A : (t.B != a && t.B != c ? t.B : t.C);

    private static SkillAtlas.Skill Make(string name, SkillChannel channel, SkillCurve curve, SkillNeed need, bool divider = false)
        => new() { Name = name, Channel = channel, IsDivider = divider, Curve = curve, BaseXp = 1f, XpExp = 0.55f, Need = need };

    private static SkillAtlas.Skill FillerSkill(int i)
        => Make("Skill " + i, SkillChannel.None, SkillCurve.ProfileB, SkillNeed.None);

    public static SkillAtlas.Skill[] DefaultSkills()
    {
        var a = SkillCurve.ProfileA;
        var b = SkillCurve.ProfileB;
        var c = SkillCurve.ProfileC;
        return new[]
        {
            Make("Locomotion", SkillChannel.Speed, b, SkillNeed.Mobility),
            Make("Sprint", SkillChannel.RunSpeed, b, SkillNeed.Danger),
            Make("Endurance", SkillChannel.MoveEnergyCost, b, SkillNeed.Mobility, true),
            Make("Strike", SkillChannel.Attack, a, SkillNeed.Aggression),
            Make("Tempo", SkillChannel.Cooldown, b, SkillNeed.Aggression, true),
            Make("Menace", SkillChannel.GroupBonus, b, SkillNeed.Aggression),
            Make("Toughness", SkillChannel.DamageTaken, b, SkillNeed.Danger, true),
            Make("Vitality", SkillChannel.Health, a, SkillNeed.Danger),
            Make("Recovery", SkillChannel.RestHeal, b, SkillNeed.Danger),
            Make("Reserves", SkillChannel.EnergyCapacity, a, SkillNeed.Hunger),
            Make("Digestion", SkillChannel.EatSpeed, b, SkillNeed.Hunger),
            Make("Scent", SkillChannel.Detection, c, SkillNeed.Hunger),
            Make("Hauling", SkillChannel.Carry, b, SkillNeed.Storage),
            Make("Storekeeping", SkillChannel.StoreEfficiency, b, SkillNeed.Storage),
            Make("Nursing", SkillChannel.Feed, b, SkillNeed.Care),
            Make("Fertility", SkillChannel.ReproduceCost, b, SkillNeed.Breeding, true),
            Make("Flight", SkillChannel.None, b, SkillNeed.None),
            Make("Swimming", SkillChannel.None, b, SkillNeed.None),
        };
    }

    public static SkillAtlas.XpSource[] DefaultXpSources() => new[]
    {
        Xp(EntityAction.MoveTo, 0, 1f), Xp(EntityAction.MoveTo, 1, 0.5f),
        Xp(EntityAction.MoveNest, 0, 1f), Xp(EntityAction.MoveNest, 2, 1f),
        Xp(EntityAction.Attack, 3, 1f), Xp(EntityAction.Attack, 4, 1f),
        Xp(EntityAction.Signal, 5, 1f), Xp(EntityAction.Signal, 6, 0.5f),
        Xp(EntityAction.Rest, 7, 1f), Xp(EntityAction.Rest, 8, 1f),
        Xp(EntityAction.Eat, 9, 1f), Xp(EntityAction.Eat, 10, 1f),
        Xp(EntityAction.Take, 12, 1f), Xp(EntityAction.Take, 11, 0.5f),
        Xp(EntityAction.Put, 13, 1f), Xp(EntityAction.Put, 2, 0.5f),
        Xp(EntityAction.Give, 14, 1f),
        Xp(EntityAction.Reproduce, 15, 1f),
    };

    private static SkillAtlas.XpSource Xp(EntityAction action, int skill, float xp)
        => new() { Action = (byte)action, Skill = (byte)skill, Xp = xp };
}
