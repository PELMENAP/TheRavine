using System.Runtime.InteropServices;

namespace TheRavine.EntityControl.Virology
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Segment
    {
        public int Start;
        public int Length;
        public ulong StrainId;
        public ulong LineageId;
        public uint InsertTick;
        public float Integrity;
        public float NetFitnessDelta;
        public float PrevFitnessDelta;
        public int TamedTicks;
        public ProteinAction DominantAction;
        public bool Tamed;

        public bool IsEndogenous => StrainId == 0UL;
    }
}