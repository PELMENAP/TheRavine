using System.Collections.Generic;

public interface ISkillData
{
    Dictionary<string, ISkill> skills { get; set; }
}