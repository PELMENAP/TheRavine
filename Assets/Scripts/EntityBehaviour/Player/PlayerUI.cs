using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class PlayerUI
{
    private Dictionary<string, PlayerSkill> playerSkillsUpdate = new Dictionary<string, PlayerSkill>();

    public void UpdateSkills(float reloadSpeed)
    {
        foreach (var item in playerSkillsUpdate)
        {
            item.Value.UpdateSkill(reloadSpeed);
        }
    }

    public void AddSkill(IEntitySkill skill, GameObject parent, Image icon, string name)
    {
        if (!playerSkillsUpdate.ContainsKey(name))
        {
            playerSkillsUpdate.Add(name, new PlayerSkill(skill, parent, icon));
        }
    }

    public void RemoveSkill(string name)
    {
        if (playerSkillsUpdate.ContainsKey(name))
        {
            playerSkillsUpdate.Remove(name);
        }
    }

    public void ActivateSkill(string name)
    {
        if (playerSkillsUpdate.ContainsKey(name))
        {
            playerSkillsUpdate[name].ActivationSkill();
        }
    }

    public void DeactivateSkill(string name)
    {
        if (playerSkillsUpdate.ContainsKey(name))
        {
            playerSkillsUpdate[name].RemoveSkill();
        }
    }

    public void UseSkill(string name, Vector2 direction, ref Vector3 playerPos)
    {
        if (playerSkillsUpdate.ContainsKey(name) && playerSkillsUpdate[name].parentSkill.activeSelf)
        {
            playerSkillsUpdate[name].UseSkill(direction, ref playerPos);
        }
        else
        {
            return;
        }
    }

    private class PlayerSkill
    {
        public GameObject parentSkill;
        private Image iconSkill;
        private IEntitySkill playerSkill;

        public PlayerSkill(IEntitySkill skill, GameObject parent, Image icon)
        {
            playerSkill = skill;
            iconSkill = icon;
            parentSkill = parent;
        }

        public void UseSkill(Vector2 direction, ref Vector3 playerPos)
        {
            if (parentSkill.activeSelf)
            {
                playerSkill.useSkill(direction, ref playerPos);
            }
        }

        public void RemoveSkill()
        {
            parentSkill.SetActive(false);
        }

        public void ActivationSkill()
        {
            parentSkill.SetActive(true);
        }

        public void UpdateSkill(float reloadSpeed)
        {
            if (parentSkill.activeSelf)
            {
                playerSkill.ReloadFields(iconSkill, reloadSpeed);
            }
        }
    }
}
