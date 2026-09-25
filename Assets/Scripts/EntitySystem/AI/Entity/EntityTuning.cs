using UnityEngine;
using Unity.Mathematics;

public enum Caste : byte
{
    Worker  = 0,
    Soldier = 1,
    Scout   = 2,
    Nurse   = 3,
    Count,
}

public readonly struct CasteModifiers
{
    public readonly float Attack;
    public readonly float Health;
    public readonly float EnergyUpkeep;
    public readonly float Speed;
    public readonly float EnergyCapacity;
    public readonly float Detection;
    public readonly float RestHeal;

    public CasteModifiers(float attack, float health, float energyUpkeep, float speed, float energyCapacity, float detection,
        float restHeal = 1f)
    {
        RestHeal       = restHeal;
        Attack         = attack;
        Health         = health;
        EnergyUpkeep   = energyUpkeep;
        Speed          = speed;
        EnergyCapacity = energyCapacity;
        Detection      = detection;
    }

    public static readonly CasteModifiers Neutral = new(1f, 1f, 1f, 1f, 1f, 1f);

    public static CasteModifiers For(Caste caste)
    {
        var r = SimulationRules.Active;
        return caste switch
        {
            Caste.Soldier => new CasteModifiers(r.SoldierAttackMul, r.SoldierHealthMul, r.SoldierUpkeepMul, 1f, 1f, 1f),
            Caste.Scout   => new CasteModifiers(1f, r.ScoutHealthMul, r.ScoutUpkeepMul, r.ScoutSpeedMul, r.ScoutEnergyCapMul, r.ScoutDetectMul),
            Caste.Nurse   => new CasteModifiers(1f, 1f, r.NurseUpkeepMul, 1f, 1f, 1f, r.NurseRestHealMul),
            _             => Neutral,
        };
    }
}

[System.Serializable]
public struct EntityTuning
{
    public float MaxHealth;
    public float MaxEnergy;
    public float EnergyRegenRate;

    public float MoveSpeed;
    public float RunSpeed;
    public float EnergyCostMoving;
    public float EnergyCostRunning;

    public float DetectionRadius;

    public float AttackRange;
    public float AttackDamage;
    public float AttackCooldown;
    public float AttackEnergyCost;

    public float ReproduceEnergyCost;
    public float ReproduceHealthCost;

    public float WanderRadius;
    public float MinWanderTime;
    public float MaxWanderTime;
    public float IdleTime;

    public LayerMask EntityLayer;
    public LayerMask FoodLayer;

    [System.NonSerialized] public float BasalDrainMul;
    [System.NonSerialized] public float DigestionMul;

    public static EntityTuning Express(in EntityTuning source, in GeneticParameters g)
        => Express(in source, in g, in CasteModifiers.Neutral);

    public static EntityTuning Express(in EntityTuning source, in GeneticParameters g, in CasteModifiers caste)
    {
        var r = SimulationRules.Active;
        var t = source;

        float speed  = g.MoveSpeedMul * caste.Speed;
        float energy = g.MaxEnergyMul * caste.EnergyCapacity;
        float metab  = g.MetabolismMul;
        float detect = g.DetectionRadiusMul * caste.Detection;

        t.MaxHealth    *= caste.Health;
        t.AttackDamage *= caste.Attack;

        float speedCost = math.pow(speed, r.GeneSpeedCostExponent);
        t.MoveSpeed         *= speed;
        t.RunSpeed          *= speed;
        t.EnergyCostMoving  *= speedCost;
        t.EnergyCostRunning *= speedCost;

        t.MaxEnergy *= energy;

        t.DigestionMul     = metab;
        t.AttackCooldown  /= math.max(metab, 1e-3f);

        t.DetectionRadius *= detect;

        t.BasalDrainMul = math.max(r.GeneMinBasalMul,
            metab
            * (1f + r.GeneEnergyCapacityUpkeep * (energy - 1f))
            * (1f + r.GeneDetectionUpkeep      * (detect - 1f)))
            * caste.EnergyUpkeep;
        return t;
    }
}