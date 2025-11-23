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
    [SerializeField] private float maxPossibleDistance;
    [SerializeField] private UnityEvent finishAction;
    [SerializeField] private InventoryItemInfo ticketInfo;
    private SyncedTimer _timer;
    private WorldDataService worldDataService;
    private UIInventory uIInventory;
    private void Start()
    {
        worldDataService = ServiceLocator.GetService<WorldDataService>();
        uIInventory = ServiceLocator.GetService<UIInventory>();

        if (textMeshPro == null)
        {
            Debug.Log("no display troll text");
            return;
        }
        int factTime = timeToDelay - 50 * worldDataService.WorldData.CurrentValue.cycleCount;
        if (factTime <= 0) factTime = 50;
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
        worldDataService.IncrementCycle();
        if(Vector3.Distance(player.position, transform.position) > maxPossibleDistance)
        {
            Destroy(gameObject);
            return;
        }

        if(uIInventory.HasItem(ticketInfo)) worldDataService.SetGameWon(true);
        
        finishAction?.Invoke();
    }

    private void OnDisable() {
        _timer.Pause();
    }
}
