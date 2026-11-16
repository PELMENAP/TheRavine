public partial class EntityModel
{
    public void AwardSkillXp(int action)
    {
        var colony = Colony;
        if (_skills == null || colony == null || colony.SkillTopology == null || (uint)ManagerIndex >= (uint)_skills.Capacity) return;
        SkillGraphXp.Award(_skills, ManagerIndex, action, SimulationClock.Time, colony.SkillTopology, SkillAtlas.Active, in SimulationRules.Frame);
    }

    private void RefreshSkillTuning()
    {
        if (_skills == null || (uint)ManagerIndex >= (uint)_skills.Capacity || !_skills.IsDirty(ManagerIndex)) return;
        _skills.ClearDirty(ManagerIndex);
        ApplyTuning();
    }

    private SkillModifiers CurrentSkillModifiers()
    {
        var colony = Colony;
        if (_skills == null || colony == null || colony.SkillTopology == null || (uint)ManagerIndex >= (uint)_skills.Capacity)
            return SkillModifiers.Neutral;
        return SkillModifiers.From(_skills, ManagerIndex, colony.SkillTopology, SkillAtlas.Active);
    }

    private void ApplyTuning()
    {
        var skills = CurrentSkillModifiers();
        _casteMods = CasteModifiers.Combine(in _roleMods, in skills);
        RestHealMul = _casteMods.RestHeal;

        var tuning = EntityTuning.Express(in _baseTuning, in Brain.Context.CoordMLP.Params, in _roleMods, in skills);
        Tuning = tuning;
        float bodyBefore = BodyEnergy;
        Stats.Rescale(tuning.MaxHealth, tuning.MaxEnergy);
        Digestion.Resize(tuning.MaxEnergy * SimulationRules.Active.StomachCapacityFraction);
        _ledger.Flows.RescaleNet += BodyEnergy - bodyBefore;
        Vectorizer?.SetMaxima(tuning.MaxHealth, tuning.MaxEnergy);
    }
}
