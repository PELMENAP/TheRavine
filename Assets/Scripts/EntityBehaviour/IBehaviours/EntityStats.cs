
public class EntityStats
{
    public int energy { get; private set; }
    public int maxEnergy { get; private set; }

    public EntityStats(int _energy, int _maxEnergy)
    {
        energy = _energy;
        maxEnergy = _maxEnergy;
        if (maxEnergy < energy)
            maxEnergy = energy;
    }

    public EntityStats(EntityStatsInfo info)
    {
        energy = info.Energy;
        maxEnergy = info.MaxEnergy;
        if (maxEnergy < energy)
            maxEnergy = energy;
    }

    public void DecreaseEnergy(int _energy)
    {
        if (_energy < 0)
            return;
        if (energy > _energy)
            energy -= _energy;
        else
            energy = 0;
    }

    public void IncreaseEnergy(int _energy)
    {
        if (_energy < 0)
            return;
        if (energy + _energy > maxEnergy)
            energy = maxEnergy;
        else
            energy += _energy;
    }
}