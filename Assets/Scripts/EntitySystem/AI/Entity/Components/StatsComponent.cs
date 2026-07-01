using R3;
using Unity.Mathematics;

public class StatsComponent : IComponent
{
    public ReactiveProperty<float> Health { get; }
    public ReactiveProperty<float> Energy { get; }
    public float MaxHealth { get; }
    public float MaxEnergy { get; }

    private float _starvationTimer;

    public void FillComponent(float maxHealth, float maxEnergy)
    {
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        Health = new ReactiveProperty<float>(maxHealth * 0.5f);
        Energy = new ReactiveProperty<float>(maxEnergy * 0.5f);
    }

    public void Tick(float deltaTime, float regenRate, bool isIdle)
    {
        if (isIdle && Energy.Value < MaxEnergy)
            Energy.Value = math.min(Energy.Value + regenRate * deltaTime, MaxEnergy);

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

    public void Dispose() { Health.Dispose(); Energy.Dispose(); }
}