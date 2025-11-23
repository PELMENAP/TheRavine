using UnityEngine;

[CreateAssetMenu(fileName = "NewSkillData", menuName = "Skills/SkillData")]
public class SkillData : ScriptableObject
{
    public string SkillName;
    [Min(0)] public int EnergyCost;
    [Min(0)] public int RechargeTime;
}