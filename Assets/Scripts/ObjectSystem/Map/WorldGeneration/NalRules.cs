using Unity.Collections;

using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public struct NalSpeciesParams
    {
        public byte Chance, Attempt, Distance, Delay;
        public byte Kind;
        public int EvolutionDense;
        public int DeathMainDense;
        public int DeathOtherStart;
        public int DeathOtherCount;
        public byte DeathFactor;
        public float DecaySeconds;

        public const byte KindDecay = 3;
    }

    public sealed class NalRules
    {
        public NativeArray<NalSpeciesParams> Species;
        public NativeArray<int> OtherDense;

        public NalRules(ObjectInfoRegistry registry)
        {
            int count = registry.Count;
            Species = new NativeArray<NalSpeciesParams>(count, Allocator.Persistent);

            int otherTotal = 0;
            for (int i = 0; i < count; i++)
            {
                SpreadPattern pattern = registry.GetDense(i).OnDeathPattern;
                if (pattern != null && pattern.other != null) otherTotal += pattern.other.Length;
            }

            OtherDense = new NativeArray<int>(otherTotal, Allocator.Persistent);

            int cursor = 0;
            for (int i = 0; i < count; i++)
            {
                ObjectInfo info = registry.GetDense(i);
                var p = new NalSpeciesParams { EvolutionDense = -1, DeathMainDense = -1 };

                NAlInfo n = info.NalInfo;
                if (info.BehaviourType == BehaviourType.GROW || (info.BehaviourType == BehaviourType.NAL && n != null))
                {
                    p.Kind = (byte)info.BehaviourType;

                    if (n != null)
                    {
                        p.Chance = n.chance;
                        p.Attempt = n.attempt;
                        p.Distance = n.distance;
                        p.Delay = n.delay;
                    }

                    if (info.EvolutionStep != null)
                        p.EvolutionDense = registry.DenseIndexOf(info.EvolutionStep.Id);

                    SpreadPattern pattern = info.OnDeathPattern;
                    if (pattern != null)
                    {
                        if (pattern.main != null)
                            p.DeathMainDense = registry.DenseIndexOf(pattern.main.Id);

                        p.DeathFactor = pattern.factor;
                        p.DeathOtherStart = cursor;

                        if (pattern.other != null)
                        {
                            for (int k = 0; k < pattern.other.Length; k++)
                            {
                                if (pattern.other[k] == null) continue;
                                int dense = registry.DenseIndexOf(pattern.other[k].Id);
                                if (dense >= 0) OtherDense[cursor++] = dense;
                            }
                        }

                        p.DeathOtherCount = cursor - p.DeathOtherStart;
                    }
                }
                else if (info.DecaySeconds > 0f)
                {
                    p.Kind = NalSpeciesParams.KindDecay;
                    p.DecaySeconds = info.DecaySeconds;
                }

                Species[i] = p;
            }
        }

        public void Dispose()
        {
            if (Species.IsCreated) Species.Dispose();
            if (OtherDense.IsCreated) OtherDense.Dispose();
        }
    }
}
