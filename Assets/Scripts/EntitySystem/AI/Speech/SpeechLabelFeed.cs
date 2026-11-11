using Unity.Mathematics;
using UnityEngine;

public sealed class SpeechLabelFeed : MonoBehaviour
{
    [SerializeField] private SpeechLexicon lexicon;

    private SpeechBus _bus;
    private bool _attached;

    public void Bind(SpeechBus bus)
    {
        Detach();
        _bus = bus;
        Attach();
    }

    private void Attach()
    {
        if (_bus == null || _attached || !isActiveAndEnabled) return;
        _bus.RetainAudible();
        _attached = true;
    }

    private void Detach()
    {
        if (!_attached) return;
        _bus.ReleaseAudible();
        _attached = false;
    }

    private void OnEnable()
    {
        if (_bus == null) ServiceLocator.Services.TryGet(out _bus);
        Attach();
    }

    private void OnDisable() => Detach();

    private void LateUpdate()
    {
        if (_bus == null)
        {
            if (ServiceLocator.Services.TryGet(out _bus)) Attach();
            return;
        }

        var audible = _bus.Audible;
        if (!audible.IsCreated) return;
        int n = audible.Length;
        if (n == 0) return;

        var lex = lexicon != null ? lexicon : SpeechLexicon.Default;
        ref readonly var r = ref SimulationRules.Frame;
        for (int i = 0; i < n; i++)
        {
            var u = audible[i];
            float radius = r.SpeechRadius * math.lerp(r.SpeechMinRadiusFraction, 1f, u.Loudness * (1f / 255f));
            DialogSystem.Instance.Publish(new Vector3(u.Pos.x, 0f, u.Pos.y), radius, lex.Label(u.Tokens, u.Len));
        }
    }
}
