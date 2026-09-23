using UnityEngine;
using Unity.Mathematics;

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

    public static EntityTuning Express(in EntityTuning source, in GeneticParameters g)
    {
        var r = SimulationRules.Active;
        var t = source;

        float speed  = g.MoveSpeedMul;
        float energy = g.MaxEnergyMul;
        float metab  = g.MetabolismMul;
        float detect = g.DetectionRadiusMul;

        float speedCost = math.pow(speed, r.GeneSpeedCostExponent);
        t.MoveSpeed         *= speed;
        t.RunSpeed          *= speed;
        t.EnergyCostMoving  *= speedCost;
        t.EnergyCostRunning *= speedCost;

        t.MaxEnergy *= energy;

        t.EnergyRegenRate *= metab;
        t.AttackCooldown  /= math.max(metab, 1e-3f);

        t.DetectionRadius *= detect;

        t.BasalDrainMul = math.max(r.GeneMinBasalMul,
            metab
            * (1f + r.GeneEnergyCapacityUpkeep * (energy - 1f))
            * (1f + r.GeneDetectionUpkeep      * (detect - 1f)));
        return t;
    }
}