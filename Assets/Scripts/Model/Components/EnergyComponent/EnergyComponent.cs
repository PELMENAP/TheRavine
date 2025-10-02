using R3;

using UnityEngine;

public interface IEnergyComponent : IComponent
{
    ReadOnlyReactiveProperty<float> Energy { get; }
    ReadOnlyReactiveProperty<float> MaxEnergy { get; }

    bool TryConsume(float amount);
    void Restore(float amount);
}

public class EnergyComponent : IEnergyComponent
{
    private readonly ReactiveProperty<float> energy;
    private readonly ReactiveProperty<float> maxEnergy;
    public ReadOnlyReactiveProperty<float> Energy { get; }
    public ReadOnlyReactiveProperty<float> MaxEnergy { get; }

    public EnergyComponent(float startEnergy, float _maxEnergy)
    {
        maxEnergy = new ReactiveProperty<float>(Mathf.Max(startEnergy, _maxEnergy)); ;
        MaxEnergy = maxEnergy.ToReadOnlyReactiveProperty();
        energy = new ReactiveProperty<float>(Mathf.Clamp(startEnergy, 0, maxEnergy.Value)); ;
        Energy = energy.ToReadOnlyReactiveProperty();
    }

    public EnergyComponent(EnergyInfo info)
    {
        maxEnergy = new ReactiveProperty<float>(Mathf.Max(info.Energy, info.MaxEnergy)); ;
        MaxEnergy = maxEnergy.ToReadOnlyReactiveProperty();
        energy = new ReactiveProperty<float>(Mathf.Clamp(info.Energy, 0, maxEnergy.Value)); ;
        Energy = energy.ToReadOnlyReactiveProperty();
    }

    public bool TryConsume(float amount)
    {
        if (amount < 0) return false;
        if (energy.Value < amount) return false;

        energy.Value -= amount;
        return true;
    }

    public void Restore(float amount)
    {
        if (amount < 0) return;
        energy.Value = Mathf.Min(maxEnergy.Value, energy.Value + amount);
    }

    public void Dispose()
    {
        Energy.Dispose();
        MaxEnergy.Dispose();
    }
}