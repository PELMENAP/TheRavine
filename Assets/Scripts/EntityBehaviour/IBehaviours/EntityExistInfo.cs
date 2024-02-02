using System.Collections.Generic;

public struct EntityExistInfo
{
    private float Energy;
    public readonly string Name;
    public readonly int PrefabID;
    public Dictionary<string, ISkill> Skills;
    public EntityExistInfo(string _name, int _prefabID, float _energy)
    {
        Name = _name;
        PrefabID = _prefabID;
        Energy = _energy;
        Skills = new Dictionary<string, ISkill>();
    }

    public float GetEnergy() => Energy;
    public void DecreaseEnergy(float energy) => Energy -= energy;
}