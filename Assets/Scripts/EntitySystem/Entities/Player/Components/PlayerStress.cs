using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public sealed class PlayerStress
    {
        public float Stress { get; private set; }
        public float Smooth { get; private set; }
        public int Level { get; private set; }

        public float Effect => CreatureCore.StressEffect(Smooth, Level, in SimulationRules.Frame);

        public void Step(float dt, float energyFraction, float environmentDrainMul, bool exposed, int alarmed, int near,
            in SimulationRules.RulesFrame r)
        {
            if (!r.StressSourcesEnabled || !r.PlayerStressEnabled)
            {
                Stress = 0f;
                Smooth = 0f;
                Level  = CreatureCore.StressCalm;
                return;
            }
            if (!(dt > 0f)) return;

            Stress *= math.exp(-dt / math.max(r.StressTau, 1e-3f));
            float added = CreatureCore.HungerStressRate(energyFraction, in r)
                        + CreatureCore.ColdStressRate(environmentDrainMul, exposed, in r)
                        + CreatureCore.ContagionStressRate(alarmed, near, in r);
            Stress = math.saturate(Stress + math.max(0f, added * dt));
            Smooth = CreatureCore.SmoothStress(Smooth, Stress, dt, in r);
            Level  = CreatureCore.NextStressLevel(Level, Smooth, in r);
        }
    }
}
