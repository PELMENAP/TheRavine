public interface ISkill
{
    string SkillName { get; set; }
    float EnergyCost { get; set; }
    float RechargeTime { get; set; }
    void Use(EntityExistInfo entity);
    bool CanUse(EntityExistInfo entity);
    void Recharge();
    float GetRechargeTime();
}