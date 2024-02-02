using UnityEngine;
public class FlyingSkill : ISkill
{
    public string SkillName { get; set; }
    public float EnergyCost { get; set; }
    public float RechargeTime { get; set; }
    private float lastUsedTime;

    public FlyingSkill(string name, float energyCost, float rechargeTime)
    {
        SkillName = name;
        EnergyCost = energyCost;
        RechargeTime = rechargeTime;
        lastUsedTime = -RechargeTime;
    }

    public void Use(EntityExistInfo entity)
    {
        if (CanUse(entity))
        {
            Debug.Log($"{entity.Name} использует навык {SkillName}");
            entity.DecreaseEnergy(EnergyCost);
            lastUsedTime = Time.time;
        }
        else
        {
            Debug.Log($"Нельзя использовать навык {SkillName} сейчас");
        }
    }

    public bool CanUse(EntityExistInfo entity)
    {
        return entity.GetEnergy() >= EnergyCost && Time.time - lastUsedTime >= RechargeTime;
    }

    public void Recharge()
    {
        lastUsedTime = -RechargeTime;
    }
    public float GetRechargeTime() => (Time.time - lastUsedTime) / RechargeTime;
}