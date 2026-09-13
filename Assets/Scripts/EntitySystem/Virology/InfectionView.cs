using System;

namespace TheRavine.EntityControl.Virology
{
    [Serializable]
    public struct InfectionView
    {
        public string StrainLabel;
        public string LineageLabel;
        public int CodonCount;
        public float Integrity;
        public int AgeTicks;
        public bool Tamed;
        public ProteinAction DominantAction;
        public float NetFitnessDelta;
    }
}