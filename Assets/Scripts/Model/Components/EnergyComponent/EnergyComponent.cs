using R3;
using UnityEngine;

public interface IEnergyComponent : IComponent
{
    ReadOnlyReactiveProperty<float> Energy { get; }
    ReadOnlyReactiveProperty<float> MaxEnergy { get; }

    bool TryConsume(float amount);
    void Restore(float amount);
}

public sealed class EnergyComponent : IEnergyComponent, System.IDisposable
{
    private readonly ReactiveProperty<float> energy;
    private readonly ReactiveProperty<float> maxEnergy;

    public ReadOnlyReactiveProperty<float> Energy => energy;
    public ReadOnlyReactiveProperty<float> MaxEnergy => maxEnergy;

    public EnergyComponent(float startEnergy, float _maxEnergy)
    {
        var validMax = Mathf.Max(0f, _maxEnergy);
        var validStart = Mathf.Clamp(startEnergy, 0f, validMax);

        maxEnergy = new ReactiveProperty<float>(validMax);
        energy = new ReactiveProperty<float>(validStart);
    }

    public EnergyComponent(EnergyInfo info) : this(info.Energy, info.MaxEnergy) {}

    public bool TryConsume(float amount)
    {
        if (amount <= 0f) return false;
        if (energy.Value < amount) return false;

        energy.Value -= amount;
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f) return;
        energy.Value = Mathf.Min(maxEnergy.Value, energy.Value + amount);
    }

    public void SetMaxEnergy(float newMax)
    {
        if (newMax < 0f) return;
        maxEnergy.Value = newMax;
        energy.Value = Mathf.Min(energy.Value, newMax);
    }

    public void Dispose()
    {
        energy.Dispose();
        maxEnergy.Dispose();
    }
}
