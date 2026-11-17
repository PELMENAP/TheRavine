using System.Text;

public static class SkillGraphText
{
    public static string ChannelLabel(SkillChannel channel) => channel switch
    {
        SkillChannel.Speed => "Move speed",
        SkillChannel.RunSpeed => "Run speed",
        SkillChannel.MoveEnergyCost => "Energy cost of moving",
        SkillChannel.Attack => "Attack damage",
        SkillChannel.Cooldown => "Attack cooldown",
        SkillChannel.GroupBonus => "Group attack bonus",
        SkillChannel.DamageTaken => "Damage taken",
        SkillChannel.Health => "Maximum health",
        SkillChannel.RestHeal => "Healing while resting",
        SkillChannel.EnergyCapacity => "Maximum energy",
        SkillChannel.EatSpeed => "Eating speed",
        SkillChannel.Detection => "Detection radius",
        SkillChannel.Carry => "Carry capacity",
        SkillChannel.StoreEfficiency => "Storage efficiency",
        SkillChannel.Feed => "Food sharing",
        SkillChannel.ReproduceCost => "Cost of reproduction",
        _ => string.Empty,
    };

    public static string Percent(float multiplier)
    {
        float change = (multiplier - 1f) * 100f;
        return (change >= 0f ? "+" : "-") + System.Math.Abs(change).ToString("0") + "%";
    }

    public static string EffectLine(SkillAtlas atlas, SkillGraphSnapshot snapshot, int slot)
    {
        int skill = snapshot.Skill[slot];
        var channel = atlas.SkillAt(skill).Channel;
        if (channel == SkillChannel.None) return "No direct effect yet";

        var state = snapshot.State[slot];
        string label = ChannelLabel(channel);
        if (state != SkillVertexState.Open && state != SkillVertexState.Available) return label + ": inactive";
        return label + " " + Percent(snapshot.Effect[slot]) + "  (next level " + Percent(snapshot.NextEffect[slot]) + ")";
    }

    public static string SourcesLine(SkillAtlas atlas, int skill)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < atlas.XpSourceCount; i++)
        {
            ref readonly var source = ref atlas.XpSourceAt(i);
            if (source.Skill != skill) continue;
            if (builder.Length > 0) builder.Append(", ");
            builder.Append(((EntityAction)source.Action).ToString());
        }
        return builder.Length > 0 ? "Gains XP from: " + builder : "No action trains this skill";
    }
}
