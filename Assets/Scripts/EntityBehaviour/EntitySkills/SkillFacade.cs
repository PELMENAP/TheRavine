using System.Collections.Generic;

public class SkillFacade
{
    private Dictionary<AEntity, Dictionary<string, ISkill>> entitySkills;

    public SkillFacade()
    {
        entitySkills = new Dictionary<AEntity, Dictionary<string, ISkill>>();
    }

    public void AddEntity(AEntity entity)
    {
        if (!entitySkills.ContainsKey(entity))
        {
            entitySkills.Add(entity, entity.Skills);
        }
    }

    public void RemoveEntity(AEntity entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            entitySkills.Remove(entity);
        }
    }

    public void AddSkillToEntity(AEntity entity, ISkill skill)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (!entitySkills[entity].ContainsKey(skill.SkillName))
            {
                entitySkills[entity].Add(skill.SkillName, skill);
            }
        }
    }

    public void RemoveSkillFromEntity(AEntity entity, string skillName)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (entitySkills[entity].ContainsKey(skillName))
            {
                entitySkills[entity].Remove(skillName);
            }
        }
    }

    public bool EntityHasSkill(AEntity entity, string skillName)
    {
        return entitySkills.ContainsKey(entity) && entitySkills[entity].ContainsKey(skillName);
    }

    public Dictionary<string, ISkill> GetEntitySkills(AEntity entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            return entitySkills[entity];
        }
        return null;
    }

    public ISkill GetEntitySkill(AEntity entity, string skillName)
    {
        if (EntityHasSkill(entity, skillName))
        {
            return entitySkills[entity][skillName];
        }
        return null;
    }
}
