using Unity.Mathematics;

public class RestCommand : EntityCommand
{
    private static readonly EntityModel[] Nearby = new EntityModel[16];
    private double _prev;
    private float  _rested;

    public RestCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        if (model.IsHungry) return Fail(SimulationRules.Active.InfeasibleActionReward);
        model.Motor.Stop();
        _prev   = SimulationClock.TimeD;
        _rested = 0f;
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        var r     = SimulationRules.Active;
        var stats = model.Stats;
        if (model.IsHungry) return Fail(r.InfeasibleActionReward);

        double end = decision.EndTime;
        double t   = math.min(SimulationClock.TimeD, end);
        float step = (float)(t - _prev);
        if (step > 0f)
        {
            _prev = t;
            _rested += step;
            float hp     = stats.Hp;
            float en     = stats.En;
            float cost   = r.RestHealEnergyCost;
            var   nest   = model.Nest;
            float aura   = model.IsAtNest && nest != null
                ? 1f + r.NurseHealAura * math.min(nest.NursesAtNest, r.NurseHealAuraMax)
                : 1f;
            float rate   = r.RestHealRate * (model.IsAtNest ? r.NestRestHealMul : 1f) * model.RestHealMul * aura;
            float spare  = math.max(0f, en - r.StarvationThreshold - r.RestMinEnergyReserve);
            float budget = cost > 0f ? spare / cost : (spare > 0f ? float.MaxValue : 0f);
            float heal   = math.min(math.min(rate * step, stats.MaxHealth - hp), budget);
            if (heal > 0f)
            {
                stats.Hp = hp + heal;
                stats.En = math.max(0f, en - heal * cost);
            }
        }

        if (t < end) return EntityCommandStatus.Running;

        float factor = RestFactor(r);
        model.Brain.Context.GoalRestScore += factor;
        return Complete(r.RestRewardPerSecond * _rested * factor);
    }

    private float RestFactor(SimulationRules r)
    {
        int found = model.Perception.FindEntitiesInRadius(model.Motor.Position(), model, r.CrowdRadius, Nearby);
        int resting = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Nearby[i];
            Nearby[i] = null;
            if (e == null || e.Colony != model.Colony || !e.IsCommandRunning || e.LastAction != EntityAction.Rest) continue;
            resting++;
        }
        float social = 1f + r.RestSocialBonus * math.min(resting, r.RestSocialMax);
        return social * (model.IsNight && model.IsAtNest ? r.RestNightMul : 1f);
    }
}
