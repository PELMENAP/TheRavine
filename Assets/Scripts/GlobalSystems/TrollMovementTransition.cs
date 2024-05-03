using UnityEngine;
using TMPro;


using TheRavine.Base;
using TheRavine.Inventory;

using UnityEngine.Events;
public class TrollMovementTransition : MonoBehaviour
{
    [SerializeField] private int timeToDelay;
    [SerializeField] private TextMeshPro textMeshPro;
    [SerializeField] private TimerType timerType;
    [SerializeField] private Transform player;
    [SerializeField] private UIInventory uiInventory;
    [SerializeField] private float maxPossibleDistance;
    [SerializeField] private UnityEvent finishAction;
    [SerializeField] private InventoryItemInfo ticketInfo;
    private SyncedTimer _timer;
    private void Start() {
        int factTime = timeToDelay - 20 * DataStorage.cycleCount;
        if(factTime <= 0) factTime = 50;
        _timer = new SyncedTimer(timerType, factTime);
        _timer.TimerValueChanged += OnTimerValueChanged;
        _timer.TimerFinished += TimerFinished;

        _timer.Start();
    }

    private void OnTimerValueChanged(float remainingSeconds, TimeChangingSource changingSource)
    {
        textMeshPro.text = remainingSeconds.ToString("F2");;
    }

    private void TimerFinished()
    {
        DataStorage.cycleCount++;
        if(Vector3.Distance(player.position, transform.position) > maxPossibleDistance)
        {
            Destroy(gameObject);
            return;
        }

        if(uiInventory.HasItem(ticketInfo.title)) DataStorage.winTheGame = true;
        
        finishAction?.Invoke();
    }

    private void OnDisable() {
        _timer.Pause();
    }
}
