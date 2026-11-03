using System;
using Unity.Mathematics;

public class SpeechComponent : IComponent
{
    public Utterance Own { get; private set; }
    public bool Muted { get; private set; }

    public readonly float[] Bag = new float[SimulationRules.SpeechVocab];
    public Utterance Loudest { get; private set; }
    public bool   HasLoudest { get; private set; }
    public float2 Dir  { get; private set; }
    public float  Dist { get; private set; }
    public float  Kin  { get; private set; }
    public float  Age  { get; private set; }

    public void SetOwn(in Utterance utterance) => Own = utterance;

    public void SetMuted(bool muted) => Muted = muted;

    public void Listen(SpeechBus bus, int index, in float2 self, float now)
    {
        Array.Clear(Bag, 0, Bag.Length);
        HasLoudest = false;
        Dir  = float2.zero;
        Dist = 0f;
        Kin  = 0f;
        Age  = 0f;
        if (bus == null || index < 0) return;

        ref readonly var r = ref SimulationRules.Frame;
        float invFresh = 1f / r.SpeechFreshSeconds;
        float best = 0f;
        var loudest = default(Utterance);

        for (int s = 0; s < SimulationRules.SpeechInbox; s++)
        {
            var u = bus.InboxAt(index, s);
            if (u.Len == 0) continue;

            float fresh = 1f - math.max(now - u.Time, 0f) * invFresh;
            if (fresh <= 0f) continue;

            float w   = u.Loudness * (1f / 255f) * fresh;
            int   len = math.min((int)u.Len, SimulationRules.SpeechLen);
            for (int t = 0; t < len; t++)
            {
                int token = u.Token(t);
                Bag[token] = math.min(1f, Bag[token] + w);
            }

            if (w <= best) continue;
            best    = w;
            loudest = u;
            HasLoudest = true;
        }

        if (!HasLoudest) return;
        Loudest = loudest;

        float2 d    = loudest.Pos - self;
        float  dist = math.length(d);
        Dir  = dist > 1e-4f ? d / dist : float2.zero;
        Dist = 1f - math.saturate(dist / math.max(r.SpeechRadius, 1e-3f));
        Kin  = loudest.IsKin ? 1f : 0f;
        Age  = math.saturate(math.max(now - loudest.Time, 0f) * invFresh);
    }

    public void Dispose() { }
}
