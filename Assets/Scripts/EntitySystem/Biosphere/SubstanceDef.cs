using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Biosphere
{
    [Serializable]
    public struct SubstanceDef
    {
        public string name;
        public int4 composition;
        public float energy;

        public SubstanceDef(string name, int4 composition, float energy)
        {
            this.name = name;
            this.composition = composition;
            this.energy = energy;
        }
    }
}
