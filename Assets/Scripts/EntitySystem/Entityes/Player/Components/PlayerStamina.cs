using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public sealed class PlayerStamina
    {
        private readonly IEnergyComponent energy;
        private readonly float staminaPerMaxEnergy;
        private readonly float capAtZeroEnergy;
        private readonly float drainFraction;
        private readonly float regenFraction;
        private readonly float recoveryDelay;
        private readonly float energyPerStamina;
        private float current;
        private float cooldown;

        public PlayerStamina(IEnergyComponent energy, float staminaPerMaxEnergy, float capAtZeroEnergy,
            float drainFraction, float regenFraction, float recoveryDelay, float energyPerStamina)
        {
            this.energy = energy;
            this.staminaPerMaxEnergy = staminaPerMaxEnergy;
            this.capAtZeroEnergy = capAtZeroEnergy;
            this.drainFraction = drainFraction;
            this.regenFraction = regenFraction;
            this.recoveryDelay = recoveryDelay;
            this.energyPerStamina = energyPerStamina;
            current = Capacity;
        }

        public float Capacity => energy.MaxEnergy.CurrentValue * staminaPerMaxEnergy;

        public float Ratio
        {
            get
            {
                float capacity = Capacity;
                return capacity > 0f ? current / capacity : 0f;
            }
        }

        public bool Step(float dt, bool wantsSprint, bool moving)
        {
            float capacity = Capacity;
            float energyRatio = EnergyRatio();
            float cap = capacity * math.lerp(capAtZeroEnergy, 1f, energyRatio);
            current = math.min(current, cap);
            cooldown = math.max(0f, cooldown - dt);

            if (wantsSprint && moving && cooldown <= 0f && current > 0f)
            {
                float fatigue = math.lerp(1.5f, 0.75f, energyRatio);
                float drain = math.min(current, capacity * drainFraction * fatigue * dt);
                if (energy.TryConsume(drain * energyPerStamina))
                {
                    current -= drain;
                    cooldown = recoveryDelay;
                    return true;
                }
            }

            if (cooldown <= 0f)
                current = math.min(cap, current + capacity * regenFraction * energyRatio * dt);
            return false;
        }

        private float EnergyRatio()
        {
            float max = energy.MaxEnergy.CurrentValue;
            return max > 0f ? energy.Energy.CurrentValue / max : 0f;
        }
    }
}
