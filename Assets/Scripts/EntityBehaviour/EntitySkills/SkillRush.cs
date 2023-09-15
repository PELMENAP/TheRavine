using UnityEngine;
using UnityEngine.UI;

public class SkillRush : IEntitySkill
{
    private float coolDown;
    private float reloadSpeed;
    private float coolDownTime = 0;
    private float power;

    public SkillRush(float _coolDown, float _reloadSpeed, float _power)
    {
        coolDown = _coolDown;
        reloadSpeed = _reloadSpeed;
        power = _power;
    }

    public void useSkill(Vector2 direction, ref Vector3 position)
    {
        if (coolDownTime < coolDown)
        {
            return;
        }
        position += new Vector3(direction.x, direction.y, 0) * power;
        coolDownTime = 0;
    }

    public void ReloadFields(Image icon, float Speed)
    {
        icon.fillAmount = coolDownTime / coolDown;
        coolDownTime += reloadSpeed * Speed;
    }
}
