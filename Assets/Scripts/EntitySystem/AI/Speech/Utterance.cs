using Unity.Mathematics;

public struct Utterance
{
    public const int  External   = -1;
    public const byte FlagKin    = 1;
    public const byte FlagReflex = 2;
    public const int  VoiceShift = 2;
    public const int  VoiceMask  = 3;

    public ushort Tokens;
    public byte   Len;
    public byte   Loudness;
    public byte   Context;
    public byte   Flags;
    public int    Speaker;
    public float2 Pos;
    public float  Time;

    public int Token(int slot)
        => (Tokens >> (slot * SimulationRules.SpeechTokenBits)) & (SimulationRules.SpeechVocab - 1);

    public bool IsReflex => (Flags & FlagReflex) != 0;
    public bool IsKin    => (Flags & FlagKin) != 0;
    public int  Voice    => (Flags >> VoiceShift) & VoiceMask;

    public static ushort SetToken(ushort tokens, int slot, int token)
    {
        int shift = slot * SimulationRules.SpeechTokenBits;
        int mask  = (SimulationRules.SpeechVocab - 1) << shift;
        return (ushort)((tokens & ~mask) | ((token & (SimulationRules.SpeechVocab - 1)) << shift));
    }

    public static ushort Repeat(int token)
    {
        ushort tokens = 0;
        for (int i = 0; i < SimulationRules.SpeechLen; i++) tokens = SetToken(tokens, i, token);
        return tokens;
    }
}
