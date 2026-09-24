using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Mathematics;

public readonly struct SpeechHash
{
    public readonly float A, B, C, D;

    private SpeechHash(float a, float b, float c, float d)
    {
        A = a; B = b; C = c; D = d;
    }

    public float4 ToFloat4() => new float4(A, B, C, D) * 2f - 1f;

    public static SpeechHash Compute(string speech)
    {
        if (string.IsNullOrEmpty(speech)) return default;

        ulong h1 = 5381UL, h2 = 2166136261UL;
        for (int i = 0; i < speech.Length; i++)
        {
            char c = speech[i];
            h1 = ((h1 << 5) + h1) ^ c;
            h2 = (h2 ^ c) * 16777619UL;
        }

        return new SpeechHash(
            (h1 & 0xFFFF) / 65535f,
            ((h1 >> 16) & 0xFFFF) / 65535f,
            (h2 & 0xFFFF) / 65535f,
            ((h2 >> 16) & 0xFFFF) / 65535f);
    }
}

public class SpeechComponent : IComponent
{
    public float4 Own   { get; private set; }
    public float4 Heard { get; private set; }
    public bool   HasHeard { get; private set; }

    private IEntityAudio _audio;

    public void Inject(IEntityAudio audio) => _audio = audio;

    public void SetOwn(float4 speech) => Own = speech;

    public void ReceiveVector(float4 speech)
    {
        Heard    = speech;
        HasHeard = true;
    }

    public void ReceiveSpeech(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        ReceiveVector(SpeechHash.Compute(message).ToFloat4());
    }

    public void ConsumeOtherSpeech()
    {
        if (!HasHeard) return;
        Heard    = float4.zero;
        HasHeard = false;
    }

    public static string Encode(float4 speech)
    {
        uint4 q = (uint4)math.round(math.saturate(speech * 0.5f + 0.5f) * 255f);
        uint packed = q.x | (q.y << 8) | (q.z << 16) | (q.w << 24);
        return packed.ToString("X8");
    }

    public UniTask PlayAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct)
        => _audio.PlaySpeechAsync(speech, health, energy, danger, timeToBreed, lastAction, nearestEnemyDist, ct);

    public void Dispose() { }
}
