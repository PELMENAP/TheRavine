using UnityEngine;

using TheRavine.Events;

public class EventBusTester : MonoBehaviour
{
    private void Awake()
    {
        EventBusByName eventBus = new EventBusByName();

        // Подписываемся на событие "DamageEvent"
        eventBus.Subscribe<int>("DamageEvent", HandleDamageEvent);

        // Вызываем событие "DamageEvent"
        eventBus.Invoke<int>("DamageEvent", 10);

        // Отписываемся от события "DamageEvent"
        eventBus.Unsubscribe<int>("DamageEvent", HandleDamageEvent);
    }

    private void HandleDamageEvent(int damage)
    {
        Debug.Log("Player took damage: " + damage);
    }
    private void HandleDamageEvent2(int damage)
    {
        Debug.Log("Player took damage: " + damage + " by other handler");
    }

}