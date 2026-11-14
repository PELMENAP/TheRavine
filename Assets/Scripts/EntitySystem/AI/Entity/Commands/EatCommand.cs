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
    private float  _toxicity;
    private float  _yield;

    public EatCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => !model.IsSated;

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        model.Motor.Stop();

        if (model.Carrying > 0f)
            return StartEating(model.TakeCarried(), 0f, FoodKind.Plant);

        var nest  = model.Nest;
        var index = model.FoodIndex;
        bool foodNear = index != null && model.CachedFoodValid && model.CachedFoodDistance <= r.EatRange;

        if (!foodNear && nest != null && model.CanEatFromNest && nest.Storage > 0f)
        {
            float cost = math.max(model.CasteMods.NestFoodCost, 1e-3f);
            float take = math.min(nest.Storage, r.NestEatPortion * cost);
            nest.Storage -= take;
            model.Colony?.Stats.RecordStorageEaten(take);
            model.Ledger.Flows.StoreLoss += take - take / cost;
            return StartEating(take / cost, 0f, FoodKind.Plant);
        }

        if (!foodNear) return Fail(ColonyStats.EatFail.NoFood);

        long cell = model.CachedFoodCell;
        model.InvalidateCachedFood();
        double now = SimulationClock.TimeD;
        if (!index.TryClaim(cell, model, now, out var claim))
        {
            float2 here = model.Position2D;
            int colonyId = model.Colony != null ? model.Colony.ColonyId : 0;
            if (!index.TryFindNearestFood(here.x, here.y, r.EatRange, out long next, out _, colonyId) || next == cell
                || !index.TryClaim(next, model, now, out claim))
                return Fail(ColonyStats.EatFail.Claim, 0f);
            cell = next;
        }
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

        return StartEating(claim.Energy, claim.Toxicity, claim.Kind);
    }

    private EntityCommandStatus StartEating(float portion, float toxicity, FoodKind kind)
    {
        if (portion <= 0f) return Fail(ColonyStats.EatFail.Empty);

        var r = SimulationRules.Active;
        _phase    = Phase.Eating;
        _portion  = portion;
        _eaten    = 0f;
        _toxicity = toxicity;
        _yield    = model.EatYield(kind);
        _start    = SimulationClock.TimeD;
        _end      = _start + math.max(r.EatSecondsPerEnergy * portion / math.max(model.CasteMods.EatSpeed, 1e-3f), 1e-3f);

        model.RegisterFitnessEvent(EntityModel.FitnessEvent.FoodEaten);
        model.Brain.Context.GoalFoodEaten++;
        float2 here = model.Position2D;
        if (kind == FoodKind.Meat && !model.Points.HasPointNear(in here, SimulationRules.Frame.PoiVisitRadius))
            model.Points.TryRemember(in here, r.RememberPointMinSpacing);
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
            return Elapsed(_end) ? Fail(ColonyStats.EatFail.LargeNoPartner) : EntityCommandStatus.Running;
        }

        Deliver();
        if (!Elapsed(_end)) return EntityCommandStatus.Running;

        if (_toxicity > 0f) model.TakeDamage(SimulationRules.Active.ToxicDamage, null, DeathCause.Toxic);
        return Complete();
    }

    private void Deliver()
    {
        double span = _end - _start;
        float frac  = span > 0d ? (float)math.saturate((SimulationClock.TimeD - _start) / span) : 1f;
        float due   = _portion * frac - _eaten;
        if (due <= 0f) return;
        model.IngestFood(due, _yield);
        _eaten += due;
    }

    private bool Finish()
    {
        float due = _portion - _eaten;
        if (due <= 0f) return false;
        model.IngestFood(due, _yield);
        _eaten = _portion;
        return true;
    }

    private EntityCommandStatus Fail(ColonyStats.EatFail reason)
    {
        model.Colony?.Stats.RecordEatFail(reason);
        return Fail();
    }

    private EntityCommandStatus Fail(ColonyStats.EatFail reason, float reward)
    {
        model.Colony?.Stats.RecordEatFail(reason);
        return Fail(reward);
    }

    protected override void OnCancel()
    {
        if (_phase == Phase.Eating && Finish() && _toxicity > 0f && !model.IsDisposed && !model.IsDeathPending)
            model.TakeDamage(SimulationRules.Active.ToxicDamage, null, DeathCause.Toxic);
        _portion = 0f;
        _eaten   = 0f;
    }
}
