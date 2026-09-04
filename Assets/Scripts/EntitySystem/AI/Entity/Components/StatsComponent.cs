using R3;
using Unity.Mathematics;

public class StatsComponent : IComponent
{
    public ReactiveProperty<float> Health { get; private set; }
    public ReactiveProperty<float> Energy { get; private set; }
    public float MaxHealth { get; private set; }
    public float MaxEnergy { get; private set; }

    private float _starvationTimer;
    private bool _filled;
    public bool IsDisposed { get; private set; }


    public void FillComponent(float maxHealth, float maxEnergy)
    {
        if (_filled) return;
        _filled = true;
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        Health = new ReactiveProperty<float>(maxHealth * 0.5f);
        Energy = new ReactiveProperty<float>(maxEnergy * 0.5f);
    }

    public void Tick(float deltaTime, float regenRate, bool isIdle,
        float starvationThreshold, float starvationDamage, float starvationEnergyReturn)
    {
        if (IsDisposed || !_filled) return;

        float health = Health.Value;
        float energy = Energy.Value;

        if (isIdle && energy < MaxEnergy)
            energy = math.min(energy + regenRate * deltaTime, MaxEnergy);

        if (energy < starvationThreshold)
        {
            _starvationTimer += deltaTime;
            if (_starvationTimer >= 1f)
            {
                health -= starvationDamage;
                energy += starvationEnergyReturn;
                _starvationTimer = 0f;
            }
        }
        else _starvationTimer = 0f;

        energy = math.clamp(energy, 0f, MaxEnergy);
        health = math.min(health, MaxHealth);

        Energy.Value = energy;
        Health.Value = health;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Health?.Dispose();
        Energy?.Dispose();
    }
}