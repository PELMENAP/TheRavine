using UnityEngine;
using TMPro;


using TheRavine.Base;
using UnityEngine.Events;
public class TrollMovementTransition : MonoBehaviour
{
    [SerializeField] private int timeToDelay;
    [SerializeField] private TextMeshPro textMeshPro;
    [SerializeField] private TimerType timerType;
    [SerializeField] private Transform player;
    [SerializeField] private float maxPossibleDistance;
    [SerializeField] private UnityEvent finishAction;
    private SyncedTimer _timer;
    private void Start() {
        _timer = new SyncedTimer(timerType, timeToDelay);
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
        
        finishAction?.Invoke();
    }

    private void OnDisable() {
        _timer.Pause();
    }
}
