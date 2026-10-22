using Unity.Mathematics;

using TheRavine.EntityControl.Virology;

public class EatCommand : EntityCommand
{
    private enum Phase { Waiting, Eating }

    private Phase  _phase;
    private float  _portion;
    private float  _eaten;
    private double _start;
    private double _end;
    private bool   _toxic;

    public EatCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => !model.IsSated;

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        model.Motor.Stop();

        if (model.Carrying > 0f)
            return StartEating(model.TakeCarried(), false);

        var nest  = model.Nest;
        var index = model.FoodIndex;
        bool foodNear = index != null && model.CachedFoodValid && model.CachedFoodDistance <= r.EatRange;

        if (!foodNear && nest != null && model.CanEatFromNest && nest.Storage > 0f)
        {
            float cost = math.max(model.CasteMods.NestFoodCost, 1e-3f);
            float take = math.min(nest.Storage, r.NestEatPortion * cost);
            nest.Storage -= take;
            model.Colony?.Stats.RecordStorageEaten(take);
            return StartEating(take / cost, false);
        }

        if (!foodNear) return Fail();

        long cell = model.CachedFoodCell;
        model.InvalidateCachedFood();
        if (!index.TryClaim(cell, model, SimulationClock.TimeD, out var claim)) return Fail();
        model.NoteFoodSite(ChunkFoodIndex.CellCenter(cell));

        if (claim.Infected && index.TryTakePayload(cell, out var payload))
        {
            model.Infection?.InfectFromPayload(model, in payload);
            ViralPayloadPool.Release(ref payload);
        }

        if (claim.Pending)
        {
            _phase = Phase.Waiting;
            _end   = HoldUntil(r.LargeFoodWindow);
            model.ConsumeReceivedFood();
            return EntityCommandStatus.Running;
        }

        return StartEating(claim.Energy, claim.Kind == FoodKind.Toxic);
    }

    private EntityCommandStatus StartEating(float portion, bool toxic)
    {
        if (portion <= 0f) return Fail();

        var r = SimulationRules.Active;
        _phase   = Phase.Eating;
        _portion = portion;
        _eaten   = 0f;
        _toxic   = toxic;
        _start   = SimulationClock.TimeD;
        _end     = _start + math.max(r.EatSecondsPerEnergy * portion / math.max(model.CasteMods.EatSpeed, 1e-3f), 1e-3f);

        model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
        model.Brain.Context.GoalFoodEaten++;
        float2 here = model.Position2D;
        model.Points.CreditEnergy(in here, portion);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (_phase == Phase.Waiting)
        {
            if (model.ConsumeReceivedFood())
            {
                model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
                model.Brain.Context.GoalFoodEaten++;
                return Complete();
            }
            return Elapsed(_end) ? Fail() : EntityCommandStatus.Running;
        }

        Deliver();
        if (!Elapsed(_end)) return EntityCommandStatus.Running;

        if (_toxic) model.TakeDamage(SimulationRules.Active.ToxicDamage, null, DeathCause.Toxic);
        return Complete();
    }

    private void Deliver()
    {
        double span = _end - _start;
        float frac  = span > 0d ? (float)math.saturate((SimulationClock.TimeD - _start) / span) : 1f;
        float due   = _portion * frac - _eaten;
        if (due <= 0f) return;
        model.Digestion.Ingest(due);
        _eaten += due;
    }

    protected override void OnCancel()
    {
        if (_phase == Phase.Eating) Deliver();
        _portion = 0f;
    }
}
