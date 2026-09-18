using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public static class StrainComposer
    {
        public const int VariantsPerAction = 8;

        private static ushort[] _variants;
        private static byte[] _variantCount;
        private static float[] _bestAmp;
        private static bool _built;

        public static bool IsBuilt => _built;

        public static void Build()
        {
            if (_built || !VirologyRuntime.IsReady) return;

            int n = ProteinTable.ActionCount;
            _variants = new ushort[n * VariantsPerAction];
            _variantCount = new byte[n];
            _bestAmp = new float[n];

            var bestDist = new float[n * VariantsPerAction];
            for (int i = 0; i < bestDist.Length; i++) bestDist[i] = float.MaxValue;

            var centroids = VirologyRuntime.Prototype;
            var bias = VirologyRuntime.CellBias;

            for (int c = 0; c <= 0xFFFF; c++)
            {
                ushort codon = (ushort)c;
                int action = CodonEmbedding.Translate(codon, centroids, bias, 1f, out float amp);
                float dist = -amp;

                int baseIdx = action * VariantsPerAction;
                int slot = -1;
                float worst = float.MinValue;

                for (int s = 0; s < VariantsPerAction; s++)
                {
                    if (bestDist[baseIdx + s] <= worst) continue;
                    worst = bestDist[baseIdx + s];
                    slot = s;
                }

                if (slot < 0 || dist >= bestDist[baseIdx + slot]) continue;

                bestDist[baseIdx + slot] = dist;
                _variants[baseIdx + slot] = codon;
                if (_variantCount[action] < VariantsPerAction) _variantCount[action]++;
                if (amp > _bestAmp[action]) _bestAmp[action] = amp;
            }

            _built = true;
        }

        public static ushort GetCodon(ProteinAction action, uint variant)
        {
            Build();
            int idx = (int)action;
            int count = _variantCount[idx];
            if (count == 0) return 0;
            return _variants[idx * VariantsPerAction + (int)(variant % (uint)count)];
        }

        public static float PeakAmp(ProteinAction action)
        {
            Build();
            return _bestAmp[(int)action];
        }

        public static int Compose(ProteinAction[] recipe, ushort[] destination, uint seed)
        {
            Build();
            var rng = new XorShift32(seed == 0u ? 1u : seed);

            int count = math.min(recipe.Length, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = GetCodon(recipe[i], rng.NextUInt());

            return count;
        }

        public static string Validate(ProteinAction[] recipe)
        {
            Build();

            var sb = new System.Text.StringBuilder();
            bool primed = false;

            for (int i = 0; i < recipe.Length; i++)
            {
                var action = recipe[i];
                int idx = (int)action;

                if (_variantCount[idx] == 0)
                    sb.AppendLine($"[{i}] {action}: ячейка пуста, кодон не синтезируется");
                else if (_bestAmp[idx] < 0.25f)
                    sb.AppendLine($"[{i}] {action}: peak amp {_bestAmp[idx]:0.00}, эффект будет слабым");

                if (ProteinTable.Descriptors[idx].RequiresPrime && !primed)
                    sb.AppendLine($"[{i}] {action}: требует Prime непосредственно перед собой");

                primed = action == ProteinAction.Prime;
            }

            return sb.Length == 0 ? "OK" : sb.ToString();
        }
    }
}