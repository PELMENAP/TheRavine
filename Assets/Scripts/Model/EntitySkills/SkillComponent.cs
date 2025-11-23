using System.Collections.Generic;

public interface ISkillComponent : IComponent
{
    Dictionary<string, ISkill> Skills { get; set; }
}

public class SkillComponent : ISkillComponent
{
    public Dictionary<string, ISkill> Skills { get; set; } = new Dictionary<string, ISkill>();
    public void Dispose()
    {
        Skills.Clear();
    }
}