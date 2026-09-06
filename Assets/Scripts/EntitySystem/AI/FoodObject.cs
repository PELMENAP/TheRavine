using UnityEngine;

public class FoodObject : MonoBehaviour
{
    private EntityManager _manager;
    private bool _claimed;

    public bool IsClaimed => _claimed;

    public void Init(EntityManager m)
    {
        _manager = m;
        _claimed = false;
    }

    public bool TryClaim()
    {
        if (_claimed) return false;
        _claimed = true;
        gameObject.SetActive(false);
        return true;
    }

    private void OnDestroy()
    {
        if (_manager != null && Application.isPlaying)
            _manager.OnFoodConsumed();
    }
}