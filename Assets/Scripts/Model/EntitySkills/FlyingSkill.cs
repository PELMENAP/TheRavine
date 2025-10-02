using UnityEngine;
public class FlyingSkill : ISkill
{
    public string SkillName { get; }
    public float EnergyCost { get; }
    public float Cooldown { get; }

    private float lastUsedTime = -Mathf.Infinity;

    public FlyingSkill(string name, float energyCost, float cooldown)
    {
        SkillName = name;
        EnergyCost = energyCost;
        Cooldown = cooldown;
    }

    public bool CanUse(IEnergyComponent energy)
    {
        bool hasEnergy = energy.Energy.CurrentValue >= EnergyCost;
        bool offCooldown = Time.time - lastUsedTime >= Cooldown;
        return hasEnergy && offCooldown;
    }

    public void Use(IEnergyComponent energy)
    {
        if (!CanUse(energy)) 
        {
            Debug.Log($"Навык {SkillName} недоступен");
            return;
        }

        if (energy.TryConsume(EnergyCost))
        {
            lastUsedTime = Time.time;
            Debug.Log($"Навык {SkillName} использован");
        }
        else
        {
            Debug.Log($"Недостаточно энергии для навыка {SkillName}");
        }
    }

    public float GetCooldownProgress()
    {
        if (Cooldown <= 0f) return 1f;
        return Mathf.Clamp01((Time.time - lastUsedTime) / Cooldown);
    }

    public void ResetCooldown()
    {
        throw new System.NotImplementedException();
    }
}