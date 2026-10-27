using System;
using Unity.Mathematics;

public class StatsComponent : IComponent, IEnergyStore
{
    public event Action HealthDepleted;
    public float MaxHealth { get; private set; }
    public float MaxEnergy { get; private set; }

    private float _starvationTimer;
    private bool _filled;
    private bool _open;
    private float _hp;
    private float _en;
    private float _health;
    private float _energy;
    public bool IsDisposed { get; private set; }

    public float Hp
    {
        get => _open ? _hp : _health;
        set { if (_open) _hp = value; else SetHealth(value); }
    }

    public float En
    {
        get => _open ? _en : _energy;
        set { if (_open) _en = value; else _energy = value; }
    }

    private void SetHealth(float value)
    {
        if (value == _health) return;
        _health = value;
        if (value <= 0f) HealthDepleted?.Invoke();
    }

    public void FillComponent(float maxHealth, float maxEnergy)
    {
        if (_filled) return;
        _filled = true;
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        _health = maxHealth * 0.5f;
        _energy = maxEnergy * 0.5f;
    }

    public void Rescale(float maxHealth, float maxEnergy)
    {
        if (!_filled || IsDisposed || maxHealth <= 0f || maxEnergy <= 0f) return;
        float hpFrac = Hp / MaxHealth;
        float enFrac = En / MaxEnergy;
        MaxHealth = maxHealth;
        MaxEnergy = maxEnergy;
        Hp = hpFrac * maxHealth;
        En = enFrac * maxEnergy;
    }

    public void Open()
    {
        if (_open || IsDisposed || !_filled) return;
        _hp   = _health;
        _en   = _energy;
        _open = true;
    }

    public void Commit()
    {
        if (!_open) return;
        _open = false;
        if (IsDisposed) return;
        _energy = _en;
        SetHealth(_hp);
    }

    public void Tick(float deltaTime, float movementEnergy, float metabolismMultiplier, float basalDrain,
        float starvationThreshold, float starvationDamage, float starvationEnergyReturn,
        out float metabolismCredit)
    {
        metabolismCredit = 0f;
        if (IsDisposed || !_filled) return;

        float health = Hp;
        float energy = En;

        float basal = basalDrain * deltaTime;
        energy          -= (basal + movementEnergy) * metabolismMultiplier;
        metabolismCredit = basal * (1f - metabolismMultiplier);

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
        HealthDepleted = null;
    }
}
