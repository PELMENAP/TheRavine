using System;

namespace TheRavine.EntityControl.Biosphere
{
    [Serializable]
    public struct ReactionDef
    {
        public int reactantA;
        public int reactantB;
        public int productA;
        public int productB;

        public ReactionDef(int reactantA, int reactantB, int productA, int productB)
        {
            this.reactantA = reactantA;
            this.reactantB = reactantB;
            this.productA = productA;
            this.productB = productB;
        }
    }
}
