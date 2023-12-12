using System;
using UnityEngine;
public class SkillBuilder
{
    public static ISkill CreateSkill(SkillData data)
    {
        Type type = Type.GetType(data.SkillName);
        if (type == null || !typeof(ISkill).IsAssignableFrom(type))
        {
            Debug.LogError($"Не удалось найти класс навыка с именем {data.SkillName}");
            return null;
        }

        ISkill skill = Activator.CreateInstance(type, new object[] { data.SkillName, data.EnergyCost, data.RechargeTime }) as ISkill;
        if (skill != null)
        {
            return skill;
        }
        else
        {
            Debug.LogError($"Ошибка при создании экземпляра класса навыка {data.SkillName}");
            return null;
        }
    }
}
