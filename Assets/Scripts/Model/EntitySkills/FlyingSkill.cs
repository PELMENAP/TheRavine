using UnityEngine;
using Unity.Mathematics;
public class FlyingSkill : ISkill
{
    public string SkillName { get; }
    public float EnergyCost { get; }
    public float Cooldown { get; }

    private float lastUsedTime = float.NegativeInfinity;

    public FlyingSkill(string name, float energyCost, float cooldown)
    {
        SkillName = name;
        EnergyCost = energyCost;
        Cooldown = cooldown;
    }

    public bool CanUse(IEnergyComponent energy)
    {
        bool hasEnergy = energy.Energy.CurrentValue >= EnergyCost;
        bool offCooldown = SimulationClock.Time - lastUsedTime >= Cooldown;
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
            lastUsedTime = SimulationClock.Time;
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
        return math.saturate((SimulationClock.Time - lastUsedTime) / Cooldown);
    }

    public void ResetCooldown()
    {
        throw new System.NotImplementedException();
    }
}