using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed class ChemistryTable
    {
        public const int Elements = 4;

        private readonly int4[]  _composition;
        private readonly float[] _energy;
        private readonly int4[]  _reactions;
        private readonly float[] _delta;

        public int SubstanceCount => _energy.Length;
        public int ReactionCount => _reactions.Length;
        public int InvalidReactions { get; }

        public ChemistryTable(SubstanceDef[] substances, ReactionDef[] reactions)
        {
            int n = substances?.Length ?? 0;
            _composition = new int4[n];
            _energy = new float[n];
            for (int i = 0; i < n; i++)
            {
                _composition[i] = math.max(substances[i].composition, int4.zero);
                _energy[i] = math.isfinite(substances[i].energy) ? substances[i].energy : 0f;
            }

            int valid = 0, invalid = 0;
            var buffer = new int4[reactions?.Length ?? 0];
            var delta = new float[buffer.Length];
            for (int k = 0; k < buffer.Length; k++)
            {
                var r = reactions[k];
                var packed = new int4(r.reactantA, r.reactantB, r.productA, r.productB);
                if (!IsBalanced(packed)) { invalid++; continue; }
                buffer[valid] = packed;
                delta[valid] = EnergyOf(packed.z) + EnergyOf(packed.w) - EnergyOf(packed.x) - EnergyOf(packed.y);
                valid++;
            }
            _reactions = new int4[valid];
            _delta = new float[valid];
            Array.Copy(buffer, _reactions, valid);
            Array.Copy(delta, _delta, valid);
            InvalidReactions = invalid;
        }

        public int4 Composition(int substance) => (uint)substance < (uint)_composition.Length ? _composition[substance] : int4.zero;

        public float Energy(int substance) => (uint)substance < (uint)_energy.Length ? _energy[substance] : 0f;

        public int4 Reaction(int k) => _reactions[k];

        public float EnergyDelta(int k) => _delta[k];

        public int ElementSubstance(int element)
        {
            var target = int4.zero;
            target[element] = 1;
            for (int i = 0; i < _composition.Length; i++)
                if (math.all(_composition[i] == target)) return i;
            return -1;
        }

        private float EnergyOf(int s) => s >= 0 ? _energy[s] : 0f;

        private bool IsBalanced(int4 r)
        {
            int n = _energy.Length;
            if (r.x < 0 || r.x >= n || r.y >= n || r.z < 0 || r.z >= n || r.w >= n) return false;
            int4 left = Composition(r.x) + (r.y >= 0 ? Composition(r.y) : int4.zero);
            int4 right = Composition(r.z) + (r.w >= 0 ? Composition(r.w) : int4.zero);
            return math.all(left == right) && math.any(left != int4.zero);
        }

        public float4 ElementsOf(ReadOnlySpan<float> pool)
        {
            float4 sum = float4.zero;
            for (int i = 0; i < pool.Length && i < _composition.Length; i++) sum += (float4)_composition[i] * pool[i];
            return sum;
        }

        public float BondEnergy(ReadOnlySpan<float> pool)
        {
            float sum = 0f;
            for (int i = 0; i < pool.Length && i < _energy.Length; i++) sum += _energy[i] * pool[i];
            return sum;
        }
    }
}
