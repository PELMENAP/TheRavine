using UnityEngine;

public static class SkillGraphTestAtlas
{
    public const int GridSide = 4;

    public static SkillAtlas BuildGrid(SkillChannel channel = SkillChannel.None, bool divider = false)
    {
        int n = GridSide * GridSide;
        var positions = new Vector2[n];
        var rows = new ulong[n];
        var skills = new SkillAtlas.Skill[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = new Vector2((i % GridSide + 0.5f) / GridSide, (i / GridSide + 0.5f) / GridSide);
            skills[i] = new SkillAtlas.Skill { Name = "S" + i, Channel = channel, IsDivider = divider, Curve = SkillCurve.ProfileB, BaseXp = 1f, XpExp = 0.55f };
        }
        for (int i = 0; i < n; i++)
        {
            if (i % GridSide + 1 < GridSide) Link(rows, i, i + 1);
            if (i / GridSide + 1 < GridSide) Link(rows, i, i + GridSide);
        }
        Link(rows, 0, 5);
        Link(rows, 1, 4);
        return SkillAtlas.Create(positions, rows, skills, new SkillAtlas.XpSource[0]);
    }

    private static void Link(ulong[] rows, int a, int b)
    {
        rows[a] |= 1ul << b;
        rows[b] |= 1ul << a;
    }
}
