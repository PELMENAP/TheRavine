using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

using TheRavine.EntityControl;
using TheRavine.Generator;

public class EntityViewModel : AEntityViewModel, IEntityMotor, IEntityFeedback,
    IDialogListener, IDialogSender, IEntityDialogHost, IEntityDeathHandler, IEntityAudio
{
    [SerializeField] private SurfaceMotor motor;

    private async void Awake()
    {
        var map = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
        motor.Inject(map);
    }

    public void OnDeath()
    {
        sr.color = Color.gray;
        DialogSystem.Instance.RemoveDialogListener(this);
        
        Destroy(gameObject, 0f);
    }
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private StringToAudioGenerator audioGenerator;

    public async UniTask PlaySpeechAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct)
    {
        await audioGenerator.PlayFromStringAsync(speech, health, energy, danger, timeToBreed,
            lastAction, nearestEnemyDist, 1, 1, ct);
    }

    public Vector3 Position => transform.position;

    public UniTask MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
        => motor.MoveToAsync(target, speed, maxDuration, energyCostPerSec, ct);

    public void Stop() => motor.Stop();
    public async UniTask FlashColor(Color c, int d)
    {
        Color orig = sr.color;
        sr.color  = c;
        await UniTask.Delay(d * 1000);
        sr.color  = orig;
    }

    protected override void OnViewUpdate() { }
    protected override void OnViewEnable() { }
    protected override void OnViewDisable() { }

    public float GetDialogDistance() => 20f;
    public Vector3 GetCurrentPosition() => transform.position;
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