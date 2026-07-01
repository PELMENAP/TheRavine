using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;

using TheRavine.EntityControl;

public class EntityViewModel : AEntityViewModel, IEntityMotor, IEntityFeedback, IDialogListener, IDialogSender
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private TextMeshPro label;

    public Vector3 Position => transform.position;

    public async UniTask MoveToAsync(Vector3 target, float speed, float maxDuration,
        float energyCostPerSec, CancellationToken ct)
    {
        var model = (EntityModel)Entity;
        float startTime = Time.time;
        while (Vector3.Distance(transform.position, target) > 0.1f && Time.time - startTime < maxDuration)
        {
            if (model.Stats.Energy.Value <= 0) break;
            var dir = (target - transform.position).normalized;
            dir.y = 0;
            rb.linearVelocity = dir * speed;
            model.Stats.Energy.Value = Mathf.Max(0, model.Stats.Energy.Value - energyCostPerSec * Time.deltaTime);
            await UniTask.Yield(ct);
        }
        rb.linearVelocity = Vector3.zero;
    }

    public void Stop() => rb.linearVelocity = Vector3.zero;
    public async UniTask FlashColor(Color c, int d)
    {
        Color orig = sr.color;
        sr.color  = c;
        await UniTask.Delay(d * 1000);
        sr.color  = orig;
    }
    public void SetLabel(string text) => label.text = text;

    protected override void OnViewUpdate() { }
    protected override void OnViewEnable() { }
    protected override void OnViewDisable() { }

    public float GetDialogDistance() => 20f;
    public Vector3 GetCurrentPosition() => transform.position;
    public void OnSpeechGet(IDialogSender sender, string message) =>
        ((EntityModel)Entity).Speech.ReceiveSpeech(message);
    public void OnDialogGetRequire() { }
}