using Unity.Mathematics;

public class GiveCommand : EntityCommand
{
    private double _holdEnd;
    private float  _given;
    private bool   _fed;

    public GiveCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        _given = 0f;
        _fed   = model.Carrying > 0f || model.Digestion.Stomach > 0f;
        return _fed ? BeginFeed(r) : BeginGroom(r);
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!Elapsed(_holdEnd)) return EntityCommandStatus.Running;
        if (!_fed) return Complete();
        var r = SimulationRules.Active;
        return Complete(r.ShareFoodReward * _given / math.max(r.EatEnergyFood, 1e-3f));
    }

    private EntityCommandStatus BeginFeed(SimulationRules r)
    {
        bool troph = Source == CommandSource.Instinct;
        var victim = model.FindHungriestKin(r.EatRange * r.KinSearchRangeMul);
        if (victim == null || math.distance(model.Position2D, victim.Position2D) > r.EatRange * r.KinSearchRangeMul) return Fail();

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio) return Fail();

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction * model.CasteMods.Feed);
        if (portion <= 0f) return Fail();

        float given = victim.Digestion.Ingest(portion);
        _given = given;
        float rest  = portion - given;
        if (rest > 0f) model.Digestion.Ingest(rest);
        if (troph && model.Nest != null)
            model.Nest.RelieveHunger(given / math.max(r.NestStorageNorm, 1e-3f) * r.TrophallaxisHungerRelief);
        model.Infection?.TryTransmitGift(model, victim);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    private EntityCommandStatus BeginGroom(SimulationRules r)
    {
        var patient = model.FindSickestKin(r.EatRange * r.KinSearchRangeMul, r.GroomMinLoad);
        if (patient == null) return Fail();

        model.Motor.Stop();
        patient.Virology?.Groom(r.GroomAmount);
        model.Infection?.TryTransmitContact(patient, model, r.GroomInfectChance);
        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }
}
