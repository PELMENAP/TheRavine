using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

public sealed class SpeechAudioPlayer : MonoBehaviour
{
    [Header("Speech audio")]
    [SerializeField, Min(1)] private int voices = 8;
    [SerializeField, Min(1f)] private float audibleRadius = 40f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Min(0f)] private float scheduleLead = 0.03f;

    [Header("Voice bank")]
    [SerializeField, Min(4000)] private int sampleRate = 11025;
    [SerializeField, Min(0.02f)] private float syllableSeconds = 0.07f;
    [SerializeField, Min(50f)] private float baseFrequency = 330f;
    [SerializeField] private float voiceStep = 3f;
    [SerializeField, Range(0.1f, 1f)] private float gain = 0.6f;

    private SpeechBus _bus;
    private SpeechVoiceBank _bank;
    private AudioSource[] _sources;
    private double[] _busyUntil;
    private float[] _score;
    private bool _grouped;
    private bool _attached;
    private AudioListener _listener;

    [Header("Diagnostics")]
    [SerializeField] private int heard;
    [SerializeField] private int outOfRange;
    [SerializeField] private int played;
    [SerializeField] private int dropped;
    [SerializeField] private bool noListener;

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

    private void Awake()
    {
        _bank = new SpeechVoiceBank(sampleRate, syllableSeconds, baseFrequency, voiceStep, gain);
        _sources = new AudioSource[voices];
        _busyUntil = new double[voices];
        _score = new float[voices];

        for (int i = 0; i < voices; i++)
        {
            var go = new GameObject("SpeechVoice");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1f;
            src.maxDistance = audibleRadius;
            _sources[i] = src;
        }
    }

    private void OnEnable()
    {
        if (_bus == null) ServiceLocator.Services.TryGet(out _bus);
        Attach();
    }

    private void OnDisable() => Detach();

    private void OnDestroy()
    {
        _bank?.Dispose();
        _bank = null;
    }

    private void LateUpdate()
    {
        if (_bus == null)
        {
            if (ServiceLocator.Services.TryGet(out _bus)) Attach();
            return;
        }

        var audible = _bus.Audible;
        int n = audible.Length;
        if (n == 0) return;

        if (!TryGetListenerPosition(out Vector3 listener))
        {
            noListener = true;
            return;
        }
        noListener = false;

        if (!_grouped) TryAssignGroup();

        double dsp = AudioSettings.dspTime;
        float gap = SimulationRules.Frame.SpeechSyllableSeconds;
        float r2 = audibleRadius * audibleRadius;

        for (int i = 0; i < n; i++)
        {
            var u = audible[i];
            float dx = u.Pos.x - listener.x;
            float dz = u.Pos.y - listener.z;
            float d2 = dx * dx + dz * dz;
            heard++;
            if (d2 > r2)
            {
                outOfRange++;
                continue;
            }

            float score = u.Loudness * (1f / 255f) * (1f - math.sqrt(d2) / audibleRadius);
            Play(in u, score, listener.y, dsp, gap);
        }
    }

    private void Play(in Utterance u, float score, float height, double dsp, float gap)
    {
        int len = math.min((int)u.Len, SimulationRules.SpeechLen);
        for (int k = 0; k < len; k++)
        {
            int v = Acquire(score, dsp);
            if (v < 0)
            {
                dropped++;
                return;
            }
            played++;

            var src = _sources[v];
            var clip = _bank.Get(u.Voice, u.Token(k));
            double start = dsp + scheduleLead + k * gap;

            src.Stop();
            src.transform.position = new Vector3(u.Pos.x, height, u.Pos.y);
            src.clip = clip;
            src.volume = volume;
            src.PlayScheduled(start);

            _busyUntil[v] = start + clip.length;
            _score[v] = score;
        }
    }

    private bool TryGetListenerPosition(out Vector3 position)
    {
        if (_listener == null || !_listener.isActiveAndEnabled) _listener = FindFirstObjectByType<AudioListener>();
        if (_listener != null)
        {
            position = _listener.transform.position;
            return true;
        }

        var cam = Camera.main;
        position = cam != null ? cam.transform.position : default;
        return cam != null;
    }

    [ContextMenu("Test/Play all tokens")]
    private void TestPlayAllTokens()
    {
        if (!Application.isPlaying || _bank == null) return;
        if (!TryGetListenerPosition(out Vector3 listener))
        {
            Debug.LogWarning("[SpeechAudioPlayer] нет AudioListener и Camera.main: звук не услышать");
            return;
        }

        double dsp = AudioSettings.dspTime;
        int vocab = SimulationRules.SpeechVocab;
        for (int t = 0; t < vocab; t++)
        {
            int v = t % _sources.Length;
            var clip = _bank.Get(0, t);
            var src = _sources[v];
            src.Stop();
            src.transform.position = listener;
            src.clip = clip;
            src.volume = volume;
            src.PlayScheduled(dsp + 0.1 + t * 0.2);
        }
        Debug.Log($"[SpeechAudioPlayer] тест: {vocab} токенов подряд у слушателя, группа микшера: {(_sources[0].outputAudioMixerGroup != null ? _sources[0].outputAudioMixerGroup.name : "нет")}");
    }

    [ContextMenu("Test/Emit through bus")]
    private void TestEmitThroughBus()
    {
        if (!Application.isPlaying || _bus == null) return;
        if (!TryGetListenerPosition(out Vector3 listener)) return;

        var u = new Utterance
        {
            Tokens   = Utterance.SetToken(Utterance.SetToken(0, 0, 3), 1, 12),
            Len      = (byte)SimulationRules.SpeechLen,
            Loudness = 255,
            Speaker  = Utterance.External,
            Pos      = new Unity.Mathematics.float2(listener.x, listener.z),
            Time     = SimulationClock.Time,
        };
        bool ok = _bus.Emit(in u);
        Debug.Log($"[SpeechAudioPlayer] тест через шину: Emit={ok}; прозвучит после ближайшей доставки, если сетка готова");
    }

    private int Acquire(float score, double dsp)
    {
        int quietest = -1;
        float quietestScore = score;
        for (int i = 0; i < _sources.Length; i++)
        {
            if (_busyUntil[i] <= dsp) return i;
            if (_score[i] >= quietestScore) continue;
            quietestScore = _score[i];
            quietest = i;
        }
        return quietest;
    }

    private void TryAssignGroup()
    {
        var service = AudioService.Instance;
        if (service == null) return;

        _grouped = true;
        var group = service.GetMixerGroup(AudioChannel.SFX);
        if (group == null) return;
        for (int i = 0; i < _sources.Length; i++) _sources[i].outputAudioMixerGroup = group;
    }
}
