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
    private bool _open;
    private float _hp;
    private float _en;
    public bool IsDisposed { get; private set; }

    public float Hp
    {
        get => _open ? _hp : Health.Value;
        set { if (_open) _hp = value; else Health.Value = value; }
    }

    public float En
    {
        get => _open ? _en : Energy.Value;
        set { if (_open) _en = value; else Energy.Value = value; }
    }

    public void FillComponent(float maxHealth, float maxEnergy)
    {
        if (_filled) return;
        _filled = true;
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        Health = new ReactiveProperty<float>(maxHealth * 0.5f);
        Energy = new ReactiveProperty<float>(maxEnergy * 0.5f);
    }

    public void Open()
    {
        if (_open || IsDisposed || !_filled) return;
        _hp   = Health.Value;
        _en   = Energy.Value;
        _open = true;
    }

    public void Commit()
    {
        if (!_open) return;
        _open = false;
        if (IsDisposed) return;
        if (_en != Energy.Value) Energy.Value = _en;
        if (_hp != Health.Value) Health.Value = _hp;
    }

    public void Tick(float deltaTime, float movementEnergy, float regenRate, float regenMultiplier,
        float metabolismMultiplier, float basalDrain, float idleRegenBasalFraction, bool isIdle,
        float starvationThreshold, float starvationDamage, float starvationEnergyReturn,
        out float regenCredit, out float metabolismCredit)
    {
        regenCredit      = 0f;
        metabolismCredit = 0f;
        if (IsDisposed || !_filled) return;

        float health = Hp;
        float energy = En;

        float basal = basalDrain * deltaTime;
        energy          -= (basal + movementEnergy) * metabolismMultiplier;
        metabolismCredit = basal * (1f - metabolismMultiplier);

        if (isIdle && energy < MaxEnergy)
        {
            float cap         = basalDrain * idleRegenBasalFraction;
            float rate        = math.min(regenRate * regenMultiplier, cap);
            float neutralRate = math.min(regenRate, cap);

            float boosted = rate        > 0f ? math.min(energy + rate        * deltaTime, MaxEnergy) : energy;
            float neutral = neutralRate > 0f ? math.min(energy + neutralRate * deltaTime, MaxEnergy) : energy;

            regenCredit = boosted - neutral;
            energy      = boosted;
        }

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

        En = math.clamp(energy, 0f, MaxEnergy);
        Hp = math.min(health, MaxHealth);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _open = false;
        Health?.Dispose();
        Energy?.Dispose();
    }
}
