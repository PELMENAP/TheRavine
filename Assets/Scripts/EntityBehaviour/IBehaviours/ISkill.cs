public interface ISkill
{
    string SkillName { get; set; }
    float EnergyCost { get; set; }
    float RechargeTime { get; set; }
    void Use(AEntity entity);
    bool CanUse(AEntity entity);
    void Recharge();
    float GetRechargeTime();
}