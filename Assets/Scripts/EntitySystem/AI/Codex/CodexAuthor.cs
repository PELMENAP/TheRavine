public static class CodexAuthor
{
    public const int None = 0;
    public const int ColonyBase = 0x40000000;

    public static int ForPlayer(ulong clientId) => -1 - (int)(clientId & 0x3FFFFFFFUL);
    public static int ForColony(int colonyId) => ColonyBase + colonyId;

    public static bool IsPlayer(int author) => author < 0;
    public static bool IsColony(int author) => author >= ColonyBase;
    public static bool IsEntity(int author) => author > 0 && author < ColonyBase;

    public static ulong ClientOf(int author) => (ulong)(-1 - author);
    public static int ColonyOf(int author) => author - ColonyBase;
}
