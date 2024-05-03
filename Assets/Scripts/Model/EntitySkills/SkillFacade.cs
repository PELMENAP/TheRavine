using System.Collections.Generic;

public class SkillFacade
{
    private Dictionary<ISkillComponent, Dictionary<string, ISkill>> entitySkills;

    public SkillFacade()
    {
        entitySkills = new Dictionary<ISkillComponent, Dictionary<string, ISkill>>();
    }

    public void AddEntity(ISkillComponent entity)
    {
        if (!entitySkills.ContainsKey(entity))
        {
            entitySkills.Add(entity, entity.Skills);
        }
    }

    public void RemoveEntity(ISkillComponent entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            entitySkills.Remove(entity);
        }
    }

    public void AddSkillToEntity(ISkillComponent entity, ISkill skill)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (!entitySkills[entity].ContainsKey(skill.SkillName))
            {
                entitySkills[entity].Add(skill.SkillName, skill);
            }
        }
    }

    public void RemoveSkillFromEntity(ISkillComponent entity, string skillName)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (entitySkills[entity].ContainsKey(skillName))
            {
                entitySkills[entity].Remove(skillName);
            }
        }
    }

    public bool EntityHasSkill(ISkillComponent entity, string skillName)
    {
        return entitySkills.ContainsKey(entity) && entitySkills[entity].ContainsKey(skillName);
    }

    public Dictionary<string, ISkill> GetEntitySkills(ISkillComponent entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            return entitySkills[entity];
        }
        return null;
    }

    public ISkill GetEntitySkill(ISkillComponent entity, string skillName)
    {
        if (EntityHasSkill(entity, skillName))
        {
            return entitySkills[entity][skillName];
        }
        return null;
    }
}
