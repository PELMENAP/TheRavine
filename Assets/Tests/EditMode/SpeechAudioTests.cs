using NUnit.Framework;
using UnityEngine;

public class SpeechAudioTests
{
    private const int SampleRate = 11025;
    private const float Seconds = 0.07f;

    private SpeechVoiceBank _bank;

    [SetUp]
    public void SetUp() => _bank = new SpeechVoiceBank(SampleRate, Seconds, 330f, 3f, 0.6f);

    [TearDown]
    public void TearDown() => _bank.Dispose();

    [Test]
    public void BankHasAudibleClipForEveryTokenAndVoice()
    {
        var buffer = new float[Mathf.CeilToInt(SampleRate * Seconds)];
        for (int voice = 0; voice < SpeechVoiceBank.VoiceClasses; voice++)
        for (int token = 0; token < SimulationRules.SpeechVocab; token++)
        {
            var clip = _bank.Get(voice, token);
            Assert.IsNotNull(clip, $"voice {voice} token {token}");
            Assert.AreEqual(buffer.Length, clip.samples);
            Assert.IsTrue(clip.GetData(buffer, 0));

            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            Assert.Greater(peak, 0.05f, $"voice {voice} token {token} беззвучен");
            Assert.LessOrEqual(peak, 1f);
        }
    }

    [Test]
    public void DifferentTokensProduceDifferentWaveforms()
    {
        var a = new float[Mathf.CeilToInt(SampleRate * Seconds)];
        var b = new float[a.Length];
        for (int t = 1; t < SimulationRules.SpeechVocab; t++)
        {
            _bank.Get(0, 0).GetData(a, 0);
            _bank.Get(0, t).GetData(b, 0);

            float diff = 0f;
            for (int i = 0; i < a.Length; i++) diff += Mathf.Abs(a[i] - b[i]);
            Assert.Greater(diff / a.Length, 0.01f, $"токены 0 и {t} неразличимы");
        }
    }

    [Test]
    public void UtteranceTokensRoundTrip()
    {
        ushort tokens = 0;
        for (int s = 0; s < SimulationRules.SpeechLen; s++)
            tokens = Utterance.SetToken(tokens, s, SimulationRules.SpeechVocab - 1 - s);

        var u = new Utterance { Tokens = tokens, Len = (byte)SimulationRules.SpeechLen };
        for (int s = 0; s < SimulationRules.SpeechLen; s++)
            Assert.AreEqual(SimulationRules.SpeechVocab - 1 - s, u.Token(s));
    }

    [Test]
    public void LexiconMapsTypedTextToTokens()
    {
        var lexicon = ScriptableObject.CreateInstance<SpeechLexicon>();
        try
        {
            int len = lexicon.Encode("t3, t12 t7", out ushort tokens);
            Assert.AreEqual(SimulationRules.SpeechLen, len);
            var u = new Utterance { Tokens = tokens, Len = (byte)len };
            Assert.AreEqual(3, u.Token(0));
            Assert.AreEqual(12, u.Token(1));
            Assert.AreEqual(0, lexicon.Encode("неизвестно", out _));
        }
        finally
        {
            Object.DestroyImmediate(lexicon);
        }
    }
}
