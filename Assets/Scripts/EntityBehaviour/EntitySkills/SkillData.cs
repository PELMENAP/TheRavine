using UnityEngine;

[CreateAssetMenu(fileName = "NewSkillData", menuName = "Skills/SkillData")]
public class SkillData : ScriptableObject
{
    public string SkillName;
    public float EnergyCost;
    public float RechargeTime;
}