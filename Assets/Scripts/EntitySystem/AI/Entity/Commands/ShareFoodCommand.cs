using Unity.Mathematics;

public class ShareFoodCommand : EntityCommand
{
    private double _holdEnd;

    public ShareFoodCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        bool troph = Source == CommandSource.Instinct;
        var victim = troph
            ? model.FindHungriestKin(r.EatRange * r.KinSearchRangeMul)
            : model.Caste == Caste.Nurse ? model.FindNearbyJuvenile(r.EatRange * r.KinSearchRangeMul) ?? model.CachedNearest : model.CachedNearest;
        if (victim == null || math.distance(model.Position2D, victim.Position2D) > r.EatRange * r.KinSearchRangeMul) return Fail();

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio) return Fail();

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction * model.CasteMods.Feed);
        if (portion <= 0f) return Fail();

        float given = victim.Digestion.Ingest(portion);
        float rest  = portion - given;
        if (rest > 0f) model.Digestion.Ingest(rest);
        if (troph && model.Nest != null)
            model.Nest.RelieveHunger(given / math.max(r.NestStorageNorm, 1e-3f) * r.TrophallaxisHungerRelief);
        model.Infection?.TryTransmitGift(model, victim);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_holdEnd) ? Complete() : EntityCommandStatus.Running;
}
