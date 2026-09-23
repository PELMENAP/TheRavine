using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

using TheRavine.EntityControl;

public class EntityViewModel : AEntityViewModel, IEntityMotor,
    IDialogListener, IDialogSender, IEntityDialogHost, IEntityDeathHandler, IEntityAudio
{
    [SerializeField] private SurfaceMotor motor;
    public void BindMotion(MotionSystem system) => motor.BindMotion(system);

    public void OnDeath()
    {
        DialogSystem.Instance.RemoveDialogListener(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    [SerializeField] private StringToAudioGenerator audioGenerator;

    public async UniTask PlaySpeechAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct)
    {
        await audioGenerator.PlayFromStringAsync(speech, health, energy, danger, timeToBreed,
            lastAction, nearestEnemyDist, 1, 1, ct);
    }

    public Vector3 Position()
    {
        if (transform == null) return Vector3.zero;
        return transform.position;
    }

    public void BeginMove(Vector3 target, float speed, float energyCostPerSec, double deadline)
        => motor.BeginMove(target, speed, energyCostPerSec, deadline);

    public bool       IsMoving => motor.IsMoving;
    public MoveResult LastMove => motor.LastMove;
    public float DrainEnergy() => motor.DrainEnergy();
    public void Stop() => motor.Stop();

    protected override void OnViewUpdate() { }
    protected override void OnViewEnable() { }
    protected override void OnViewDisable() { }

    public float GetDialogDistance() => 20f;
    public Vector3 GetCurrentPosition()
    {
        if (transform == null) return Vector3.zero;
        return transform.position;
    }
    public void OnSpeechGet(IDialogSender sender, string message) =>
        ((EntityModel)Entity).Speech.ReceiveSpeech(message);
    public void OnDialogGetRequire() { }
    public override void OnNetworkSpawn() { }

    private void OnEnable()  => DialogSystem.Instance.AddDialogListener(this);
    private void OnDisable() => DialogSystem.Instance.RemoveDialogListener(this);

    public void RegisterDialog(IDialogListener l)   => DialogSystem.Instance.AddDialogListener(l);
    public void UnregisterDialog(IDialogListener l)  => DialogSystem.Instance.RemoveDialogListener(l);
    public void UpdateDialogPosition(IDialogListener l) => DialogSystem.Instance.UpdateListenerPosition(l);
}