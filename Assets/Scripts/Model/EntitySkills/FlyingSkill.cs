using UnityEngine;
public class FlyingSkill : ISkill
{
    public string SkillName { get; set; }
    public int EnergyCost { get; set; }
    public int RechargeTime { get; set; }
    private float lastUsedTime;

    public FlyingSkill(string name, int energyCost, int rechargeTime)
    {
        SkillName = name;
        EnergyCost = energyCost;
        RechargeTime = rechargeTime;
        lastUsedTime = -RechargeTime;
    }

    public void Use(IMainComponent mainComponent)
    {
        if (CanUse(mainComponent))
        {
            Debug.Log($"{mainComponent.GetEntityStats()} использует навык {SkillName}");
            mainComponent.GetEntityStats().DecreaseEnergy(EnergyCost);
            lastUsedTime = Time.time;
        }
        else
        {
            Debug.Log($"Нельзя использовать навык {SkillName} сейчас");
        }
    }

    public bool CanUse(IMainComponent mainComponent)
    {
        return mainComponent.GetEntityStats().energy >= EnergyCost && Time.time - lastUsedTime >= RechargeTime;
    }

    public void Recharge()
    {
        lastUsedTime = -RechargeTime;
    }
    public float GetRechargeTime()
    {
        float del = (Time.time - lastUsedTime) / RechargeTime;
        return del > 1f ? 1f : del;
    }
}