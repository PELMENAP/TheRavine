using R3;
using Unity.Mathematics;
using UnityEngine;

public class StatsComponent : IComponent
{
    public ReactiveProperty<float> Health { get; private set; }
    public ReactiveProperty<float> Energy { get; private set; }
    public float MaxHealth { get; private set; }
    public float MaxEnergy { get; private set; }

    private float _starvationTimer;
    private bool _filled;

    public void FillComponent(float maxHealth, float maxEnergy)
    {
        if (_filled) return;
        _filled = true;
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        Health = new ReactiveProperty<float>(maxHealth * 0.5f);
        Energy = new ReactiveProperty<float>(maxEnergy * 0.5f);
    }

    public void Tick(float deltaTime, float regenRate, bool isIdle)
    {
        if (isIdle && Energy.Value < MaxEnergy)
        {
            Energy.Value = math.min(Energy.Value + regenRate * deltaTime, MaxEnergy);
            Debug.Log($"[Stats] regen tick, isIdle={isIdle}, energy={Energy.Value}");
        }

        if (Energy.Value < 5f)
        {
            _starvationTimer += deltaTime;
            if (_starvationTimer >= 1f)
            {
                Health.Value -= 15f;
                Energy.Value += 5f;
                _starvationTimer = 0f;
            }
        }
        else _starvationTimer = 0f;
    }

    public void Dispose() { Health?.Dispose(); Energy?.Dispose(); }
}