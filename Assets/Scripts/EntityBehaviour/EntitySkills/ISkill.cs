public interface ISkill
{
    string SkillName { get; set; }
    int EnergyCost { get; set; }
    int RechargeTime { get; set; }
    void Use(IMainData entity);
    bool CanUse(IMainData entity);
    void Recharge();
    float GetRechargeTime();
}