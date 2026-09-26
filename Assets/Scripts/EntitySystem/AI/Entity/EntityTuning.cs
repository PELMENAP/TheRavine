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

public struct CasteModifiers
{
    public float Attack;
    public float Health;
    public float EnergyUpkeep;
    public float Speed;
    public float EnergyCapacity;
    public float Detection;
    public float RestHeal;
    public float Cooldown;
    public float GroupBonus;
    public float NestFoodCost;
    public float EatSpeed;
    public float Carry;
    public float StoreEfficiency;
    public float DamageTaken;
    public float LevyAlpha;
    public float PoiWeight;
    public float Feed;

    public static CasteModifiers Neutral => new()
    {
        Attack = 1f, Health = 1f, EnergyUpkeep = 1f, Speed = 1f, EnergyCapacity = 1f, Detection = 1f,
        RestHeal = 1f, Cooldown = 1f, GroupBonus = 1f, NestFoodCost = 1f, EatSpeed = 1f, Carry = 1f,
        StoreEfficiency = 1f, DamageTaken = 1f, LevyAlpha = 1f, PoiWeight = 1f, Feed = 1f,
    };

    public static CasteModifiers For(Caste caste)
    {
        var r = SimulationRules.Active;
        var m = Neutral;
        if (caste != Caste.Worker) m.StoreEfficiency = r.NonWorkerStoreEfficiency;
        switch (caste)
        {
            case Caste.Soldier:
                m.Attack       = r.SoldierAttackMul;
                m.Health       = r.SoldierHealthMul;
                m.EnergyUpkeep = r.SoldierUpkeepMul;
                m.Cooldown     = r.SoldierCooldownMul;
                m.GroupBonus   = r.SoldierGroupBonusMul;
                m.NestFoodCost = r.SoldierNestFoodCostMul;
                break;
            case Caste.Worker:
                m.EatSpeed    = r.WorkerEatSpeedMul;
                m.Carry       = r.WorkerCarryMul;
                m.DamageTaken = r.WorkerDamageTakenMul;
                break;
            case Caste.Scout:
                m.Health         = r.ScoutHealthMul;
                m.EnergyUpkeep   = r.ScoutUpkeepMul;
                m.Speed          = r.ScoutSpeedMul;
                m.EnergyCapacity = r.ScoutEnergyCapMul;
                m.Detection      = r.ScoutDetectMul;
                m.LevyAlpha      = r.ScoutLevyAlphaMul;
                m.PoiWeight      = r.ScoutPoiWeightMul;
                m.Carry          = r.ScoutCarryMul;
                break;
            case Caste.Nurse:
                m.EnergyUpkeep = r.NurseUpkeepMul;
                m.RestHeal     = r.NurseRestHealMul;
                m.Speed        = r.NurseSpeedMul;
                m.Feed         = r.NurseFeedMul;
                break;
        }
        return m;
    }

    public static CasteModifiers Predator()
    {
        var r = SimulationRules.Active;
        var m = Neutral;
        m.Attack       = r.PredatorDamageMul;
        m.Health       = r.PredatorHealthMul;
        m.Speed        = r.PredatorSpeedMul;
        m.Detection    = r.PredatorDetectMul;
        m.EnergyUpkeep = r.PredatorUpkeepMul;
        m.Carry        = 0f;
        return m;
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
        => Express(in source, in g, CasteModifiers.Neutral);

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
        t.AttackCooldown  *= caste.Cooldown;

        t.DetectionRadius *= detect;

        t.BasalDrainMul = math.max(r.GeneMinBasalMul,
            metab
            * (1f + r.GeneEnergyCapacityUpkeep * (energy - 1f))
            * (1f + r.GeneDetectionUpkeep      * (detect - 1f)))
            * caste.EnergyUpkeep;
        return t;
    }
}