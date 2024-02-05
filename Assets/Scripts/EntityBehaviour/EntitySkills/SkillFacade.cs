using System.Collections.Generic;

public class SkillFacade
{
    private Dictionary<ISkillData, Dictionary<string, ISkill>> entitySkills;

    public SkillFacade()
    {
        entitySkills = new Dictionary<ISkillData, Dictionary<string, ISkill>>();
    }

    public void AddEntity(ISkillData entity)
    {
        if (!entitySkills.ContainsKey(entity))
        {
            entitySkills.Add(entity, entity.skills);
        }
    }

    public void RemoveEntity(ISkillData entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            entitySkills.Remove(entity);
        }
    }

    public void AddSkillToEntity(ISkillData entity, ISkill skill)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (!entitySkills[entity].ContainsKey(skill.SkillName))
            {
                entitySkills[entity].Add(skill.SkillName, skill);
            }
        }
    }

    public void RemoveSkillFromEntity(ISkillData entity, string skillName)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (entitySkills[entity].ContainsKey(skillName))
            {
                entitySkills[entity].Remove(skillName);
            }
        }
    }

    public bool EntityHasSkill(ISkillData entity, string skillName)
    {
        return entitySkills.ContainsKey(entity) && entitySkills[entity].ContainsKey(skillName);
    }

    public Dictionary<string, ISkill> GetEntitySkills(ISkillData entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            return entitySkills[entity];
        }
        return null;
    }

    public ISkill GetEntitySkill(ISkillData entity, string skillName)
    {
        if (EntityHasSkill(entity, skillName))
        {
            return entitySkills[entity][skillName];
        }
        return null;
    }
}
