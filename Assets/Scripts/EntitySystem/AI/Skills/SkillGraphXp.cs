public static class SkillGraphXp
{
    public static int Award(SkillGraphStore store, int index, int action, float now, SkillGraphTopology topology, SkillAtlas atlas,
        in SimulationRules.RulesFrame rules)
    {
        if (!atlas.TryGetXpRange(action, out int start, out int length)) return 0;
        int changed = 0;
        for (int i = 0; i < length; i++)
        {
            ref readonly var source = ref atlas.XpSourceAt(start + i);
            if (store.AddXp(index, source.Skill, source.Xp, now, topology, atlas, in rules)) changed++;
        }
        return changed;
    }
}
