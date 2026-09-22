using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    [CreateAssetMenu(fileName = "StrainCodonTable", menuName = "Simulation/Strain Codon Table")]
    public sealed class StrainCodonTable : ScriptableObject
    {
        [SerializeField] private uint  prototypeSeed;
        [SerializeField] private int   actionCount;
        [SerializeField] private int   variantsPerAction;
        [SerializeField] private int[] variants;
        [SerializeField] private int[] variantCount;
        [SerializeField] private float[] bestAmp;

        public uint  PrototypeSeed     => prototypeSeed;
        public int   ActionCount       => actionCount;
        public int   VariantsPerAction => variantsPerAction;

        public bool Matches =>
            prototypeSeed == VirologyRuntime.PrototypeSeed &&
            actionCount == ProteinTable.ActionCount &&
            variantsPerAction == StrainComposer.VariantsPerAction &&
            variants != null && variants.Length == actionCount * variantsPerAction &&
            variantCount != null && variantCount.Length == actionCount &&
            bestAmp != null && bestAmp.Length == actionCount;

        public void Load(ushort[] dstVariants, byte[] dstCount, float[] dstAmp)
        {
            for (int i = 0; i < variants.Length; i++) dstVariants[i] = (ushort)variants[i];
            for (int i = 0; i < variantCount.Length; i++) dstCount[i] = (byte)variantCount[i];
            System.Array.Copy(bestAmp, dstAmp, bestAmp.Length);
        }

        public void Store(uint seed, ushort[] srcVariants, byte[] srcCount, float[] srcAmp)
        {
            prototypeSeed     = seed;
            actionCount       = ProteinTable.ActionCount;
            variantsPerAction = StrainComposer.VariantsPerAction;

            variants = new int[srcVariants.Length];
            for (int i = 0; i < srcVariants.Length; i++) variants[i] = srcVariants[i];

            variantCount = new int[srcCount.Length];
            for (int i = 0; i < srcCount.Length; i++) variantCount[i] = srcCount[i];

            bestAmp = (float[])srcAmp.Clone();
        }
    }
}