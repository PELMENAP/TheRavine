public interface ISkill
{
    string SkillName { get; set; }
    int EnergyCost { get; set; }
    int RechargeTime { get; set; }
    void Use(IMainComponent mainComponent);
    bool CanUse(IMainComponent mainComponent);
    void Recharge();
    float GetRechargeTime();
}