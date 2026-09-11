using Cysharp.Threading.Tasks;
using System.Threading;

public readonly struct SpeechHash
{
    public readonly float A, B, C, D;

    private SpeechHash(float a, float b, float c, float d)
    {
        A = a; B = b; C = c; D = d;
    }

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
    public string OwnSpeech { get; private set; } = "";
    public string OtherSpeech { get; private set; } = "";

    public SpeechHash OwnSpeechHash { get; private set; }
    public SpeechHash OtherSpeechHash { get; private set; }

    private IEntityAudio _audio;

    public void Inject(IEntityAudio audio) => _audio = audio;

    public void SetOwnSpeech(string speech)
    {
        if (string.Equals(OwnSpeech, speech)) return;
        OwnSpeech = speech ?? "";
        OwnSpeechHash = SpeechHash.Compute(OwnSpeech);
    }

    public void ReceiveSpeech(string message)
    {
        if (string.Equals(OtherSpeech, message)) return;
        OtherSpeech = message ?? "";
        OtherSpeechHash = SpeechHash.Compute(OtherSpeech);
    }

    public void ConsumeOtherSpeech()
    {
        if (OtherSpeech.Length == 0) return;
        OtherSpeech = "";
        OtherSpeechHash = default;
    }

    public UniTask PlayAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct)
        => _audio.PlaySpeechAsync(speech, health, energy, danger, timeToBreed, lastAction, nearestEnemyDist, ct);

    public void Dispose() { }
}