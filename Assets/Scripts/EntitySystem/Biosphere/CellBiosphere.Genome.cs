using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed partial class CellBiosphere
    {
        public Span<float> GeneWeights(int org) => new Span<float>(_geneW, org * Genes * Inputs, Genes * Inputs);

        public Span<byte> GeneTargets(int org) => new Span<byte>(_geneT, org * Genes, Genes);

        private void RandomGenome(int o)
        {
            var w = GeneWeights(o);
            for (int k = 0; k < w.Length; k++) w[k] = RavineRandom.RangeFloat(-1f, 1f);
            var t = GeneTargets(o);
            for (int g = 0; g < t.Length; g++) t[g] = (byte)RavineRandom.RangeInt(0, TargetCount);
            var a = ActivityOf(o);
            for (int k = 0; k < a.Length; k++) a[k] = RavineRandom.RangeFloat();
        }

        private void InheritGenome(int parent, int child)
        {
            GeneWeights(parent).CopyTo(GeneWeights(child));
            GeneTargets(parent).CopyTo(GeneTargets(child));
            ActivityOf(parent).CopyTo(ActivityOf(child));

            var w = GeneWeights(child);
            for (int k = 0; k < w.Length; k++)
                if (RavineRandom.RangeFloat() < _rules.geneMutation)
                    w[k] += RavineRandom.RangeFloat(-1f, 1f) * _rules.geneMutationScale;
            var t = GeneTargets(child);
            for (int g = 0; g < t.Length; g++)
                if (RavineRandom.RangeFloat() < _rules.targetMutation)
                    t[g] = (byte)RavineRandom.RangeInt(0, TargetCount);
            var a = ActivityOf(child);
            for (int k = 0; k < a.Length; k++)
                if (RavineRandom.RangeFloat() < _rules.activityMutation)
                    a[k] = math.saturate(a[k] + RavineRandom.RangeFloat(-1f, 1f) * _rules.activityMutationScale);
        }
    }
}
