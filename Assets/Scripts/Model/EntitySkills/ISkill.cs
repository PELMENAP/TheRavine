public interface ISkill
{
    string SkillName { get; }
    float EnergyCost { get; }
    float Cooldown { get; }
    bool CanUse(IEnergyComponent energy);
    void Use(IEnergyComponent energy);
    float GetCooldownProgress();
    void ResetCooldown();
}