using UnityEngine;
using UnityEngine.UI;
public interface IEntitySkill
{
    void useSkill(Vector2 direction, ref Vector3 position);
    void ReloadFields(Image icon, float reloadSpeed);
}
