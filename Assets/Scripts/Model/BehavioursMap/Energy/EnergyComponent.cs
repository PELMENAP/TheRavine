using R3;
using UnityEngine;

public interface IEnergyComponent : IComponent
{
    ReadOnlyReactiveProperty<float> Energy { get; }
    ReadOnlyReactiveProperty<float> MaxEnergy { get; }

    bool TryConsume(float amount);
    void Restore(float amount);
}

public interface IEnergyStore
{
    float En { get; set; }
    float MaxEnergy { get; }
}

public sealed class EnergyComponent : IEnergyComponent, System.IDisposable
{
    private readonly IEnergyStore store;
    private readonly ReactiveProperty<float> energy;
    private readonly ReactiveProperty<float> maxEnergy;

    public ReadOnlyReactiveProperty<float> Energy => energy;
    public ReadOnlyReactiveProperty<float> MaxEnergy => maxEnergy;

    public EnergyComponent(IEnergyStore store)
    {
        this.store = store;
        maxEnergy = new ReactiveProperty<float>(store.MaxEnergy);
        energy = new ReactiveProperty<float>(store.En);
    }

    public bool TryConsume(float amount)
    {
        if (amount <= 0f) return false;
        if (store.En < amount) return false;

        store.En -= amount;
        Sync();
        return true;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f) return;
        store.En = Mathf.Min(store.MaxEnergy, store.En + amount);
        Sync();
    }

    public void Sync()
    {
        maxEnergy.Value = store.MaxEnergy;
        energy.Value = store.En;
    }

    public void Dispose()
    {
        energy.Dispose();
        maxEnergy.Dispose();
    }
}
