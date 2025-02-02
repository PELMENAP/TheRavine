using UnityEngine;

using TheRavine.Events;

public class EventBusTester : MonoBehaviour
{
    private void Awake()
    {
        EventBusByName eventBus = new EventBusByName();

        // Подписываемся на событие "DamageEvent"
        eventBus.Subscribe<int>(nameof(PickUpEvent), HandleDamageEvent);
        eventBus.Subscribe<int>(nameof(PickUpEvent), HandleDamageEvent2);

        // Вызываем событие "DamageEvent"
        eventBus.Invoke<int>(nameof(PickUpEvent), 10);

        // Отписываемся от события "DamageEvent"
        eventBus.Dispose();
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