using System.Collections.Generic;
public class AEntity
{
    public string Name { get; set; }
    public float Energy { get; set; }
    public float MaxEnergy { get; set; }
    public Dictionary<string, ISkill> Skills { get; set; }
    public AEntity(string name, float maxEnergy)
    {
        Name = name;
        MaxEnergy = maxEnergy;
        Energy = MaxEnergy;
        Skills = new Dictionary<string, ISkill>();
    }
}
