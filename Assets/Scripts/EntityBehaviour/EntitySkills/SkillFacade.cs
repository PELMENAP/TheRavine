using System.Collections.Generic;

public class SkillFacade
{
    private Dictionary<EntityExistInfo, Dictionary<string, ISkill>> entitySkills;

    public SkillFacade()
    {
        entitySkills = new Dictionary<EntityExistInfo, Dictionary<string, ISkill>>();
    }

    public void AddEntity(EntityExistInfo entity)
    {
        if (!entitySkills.ContainsKey(entity))
        {
            entitySkills.Add(entity, entity.Skills);
        }
    }

    public void RemoveEntity(EntityExistInfo entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            entitySkills.Remove(entity);
        }
    }

    public void AddSkillToEntity(EntityExistInfo entity, ISkill skill)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (!entitySkills[entity].ContainsKey(skill.SkillName))
            {
                entitySkills[entity].Add(skill.SkillName, skill);
            }
        }
    }

    public void RemoveSkillFromEntity(EntityExistInfo entity, string skillName)
    {
        if (entitySkills.ContainsKey(entity))
        {
            if (entitySkills[entity].ContainsKey(skillName))
            {
                entitySkills[entity].Remove(skillName);
            }
        }
    }

    public bool EntityHasSkill(EntityExistInfo entity, string skillName)
    {
        return entitySkills.ContainsKey(entity) && entitySkills[entity].ContainsKey(skillName);
    }

    public Dictionary<string, ISkill> GetEntitySkills(EntityExistInfo entity)
    {
        if (entitySkills.ContainsKey(entity))
        {
            return entitySkills[entity];
        }
        return null;
    }

    public ISkill GetEntitySkill(EntityExistInfo entity, string skillName)
    {
        if (EntityHasSkill(entity, skillName))
        {
            return entitySkills[entity][skillName];
        }
        return null;
    }
}
