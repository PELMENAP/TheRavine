using UnityEngine;

public class FoodObject : MonoBehaviour
{
    private EntityManager _manager;
    public void Init(EntityManager m) => _manager = m;

    private void OnDestroy()
    {
        if (_manager != null && Application.isPlaying)
            _manager.OnFoodConsumed();
    }
}