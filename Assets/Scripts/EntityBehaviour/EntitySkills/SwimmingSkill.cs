using UnityEngine;
public class SwimmingSkill : ISkill
{
    public string SkillName { get; set; }
    public int EnergyCost { get; set; }
    public int RechargeTime { get; set; }
    private float lastUsedTime;

    public SwimmingSkill(string name, int energyCost, int rechargeTime)
    {
        SkillName = name;
        EnergyCost = energyCost;
        RechargeTime = rechargeTime;
        lastUsedTime = -RechargeTime;
    }

    public void Use(IMainData entity)
    {
        if (CanUse(entity))
        {
            Debug.Log($"{entity.name} использует навык {SkillName}");
            entity.stats.DecreaseEnergy(EnergyCost);
            lastUsedTime = Time.time;
        }
        else
        {
            Debug.Log($"Нельзя использовать навык {SkillName} сейчас");
        }
    }

    public bool CanUse(IMainData entity)
    {
        return entity.stats.energy >= EnergyCost && Time.time - lastUsedTime >= RechargeTime;
    }

    public void Recharge()
    {
        lastUsedTime = -RechargeTime;
    }
    public float GetRechargeTime() => (Time.time - lastUsedTime) / RechargeTime;
}
