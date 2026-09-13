using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyDebugSeeder : MonoBehaviour
    {
        [SerializeField] private EntityManager manager;
        [SerializeField] private Transform probeOrigin;
        [SerializeField] private float probeRadius = 30f;

        [SerializeField]
        private ProteinAction[] recipe =
        {
            ProteinAction.DrainEnergy,
            ProteinAction.Junk,
            ProteinAction.MutationRateUp,
            ProteinAction.Junk,
            ProteinAction.Reinforce,
            ProteinAction.Junk,
            ProteinAction.Prime,
            ProteinAction.SpreadToOther,
            ProteinAction.Junk,
            ProteinAction.DrainHealth,
            ProteinAction.Junk,
            ProteinAction.Junk
        };

        [SerializeField] private string validation;
        [SerializeField] private string lastResult;

        [ContextMenu("Validate Recipe")]
        private void ValidateRecipe() => validation = StrainComposer.Validate(recipe);

        [ContextMenu("Infect Nearest")]
        private void InfectNearest()
        {
            var target = ResolveNearest();
            if (target == null) { lastResult = "цель не найдена"; return; }
            lastResult = manager.SeedProbeStrain(target, recipe) ? "вставлен" : "отказ";
        }

        [ContextMenu("Infect Random")]
        private void InfectRandom()
        {
            var target = manager.GetRandomEntity();
            if (target == null) { lastResult = "популяция пуста"; return; }
            lastResult = manager.SeedProbeStrain(target, recipe) ? "вставлен" : "отказ";
        }

        private EntityModel ResolveNearest()
        {
            if (manager == null || probeOrigin == null) return null;

            EntityModel best = null;
            float minD = probeRadius * probeRadius;
            Vector3 origin = probeOrigin.position;

            var entities = manager.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e == null || e.IsDisposed) continue;
                float d = (e.Motor.Position() - origin).sqrMagnitude;
                if (d >= minD) continue;
                minD = d;
                best = e;
            }
            return best;
        }
    }
}