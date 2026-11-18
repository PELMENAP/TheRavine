namespace TheRavine.EntityControl.Virology
{
    public static class VirologyEvents
    {
        public static long Infections { get; private set; }
        public static long Recoveries { get; private set; }
        public static long Blocked    { get; private set; }

        public static void Reset()
        {
            Infections = 0;
            Recoveries = 0;
            Blocked    = 0;
        }

        public static void NoteInfection() => Infections++;
        public static void NoteRecovery()  => Recoveries++;
        public static void NoteBlocked()   => Blocked++;
    }
}
