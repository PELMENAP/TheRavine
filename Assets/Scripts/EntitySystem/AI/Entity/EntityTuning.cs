using UnityEngine;

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
}