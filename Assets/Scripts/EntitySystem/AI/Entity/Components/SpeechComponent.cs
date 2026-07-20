using Cysharp.Threading.Tasks;
using System.Threading;
public class SpeechComponent : IComponent
{
    public string OwnSpeech { get; private set; } = "";
    public string OtherSpeech { get; private set; } = "";

    private IEntityAudio _audio;

    public void Inject(IEntityAudio audio) => _audio = audio;

    public void SetOwnSpeech(string speech) => OwnSpeech = speech;
    public void ReceiveSpeech(string message) => OtherSpeech = message;
    public void ConsumeOtherSpeech() => OtherSpeech = "";

    public UniTask PlayAsync(string speech, float health, float energy, float danger,
        float timeToBreed, int lastAction, float nearestEnemyDist, CancellationToken ct)
        => _audio.PlaySpeechAsync(speech, health, energy, danger, timeToBreed, lastAction, nearestEnemyDist, ct);

    public void Dispose() { }
}