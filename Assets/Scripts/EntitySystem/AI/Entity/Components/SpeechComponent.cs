public class SpeechComponent : IComponent
{
    public string OwnSpeech { get; private set; } = "";
    public string OtherSpeech { get; private set; } = "";

    public void SetOwnSpeech(string speech) => OwnSpeech = speech;
    public void ReceiveSpeech(string message) => OtherSpeech = message;
    public void ConsumeOtherSpeech() => OtherSpeech = "";

    public void Dispose() { }
}