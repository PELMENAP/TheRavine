using Unity.Mathematics;

public class GiveCommand : EntityCommand
{
    private double _holdEnd;
    private float  _given;
    private bool   _fed;
    private EntityModel _comfort;
    private float  _needEnergy;

    public GiveCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        var r = SimulationRules.Active;
        _given   = 0f;
        _comfort = null;
        _fed     = model.Carrying > 0f || model.Digestion.Stomach > 0f;

        if (!SimulationRules.Frame.SocialReliefEnabled)
        {
            if (!_fed) return BeginGroom(r);
            return TryBeginFeed(r, out var failure) ? EntityCommandStatus.Running : FailInteraction(failure);
        }

        ColonyStats.Interaction feedFailure = ColonyStats.Interaction.GroomNoPatient;
        if (_fed)
        {
            if (TryBeginFeed(r, out feedFailure)) return EntityCommandStatus.Running;
            _fed = false;
        }

        var patient = model.FindSickestKin(r.EatRange * r.KinSearchRangeMul, r.GroomMinLoad);
        if (patient != null) return BeginGroom(r, patient);
        if (TryBeginComfort(r)) return EntityCommandStatus.Running;
        return FailInteraction(feedFailure);
    }

    protected override EntityCommandStatus OnTick(float dt)
    {
        if (!Elapsed(_holdEnd)) return EntityCommandStatus.Running;
        var r = SimulationRules.Active;
        if (_comfort != null) return FinishComfort(r);
        if (!_fed) return Complete();
        return Complete(r.ShareFoodReward * _given / math.max(r.EatEnergyFood, 1e-3f));
    }

    private bool TryBeginFeed(SimulationRules r, out ColonyStats.Interaction failure)
    {
        failure = ColonyStats.Interaction.FeedNoTarget;
        bool troph = Source == CommandSource.Instinct;
        var victim = model.FindHungriestKin(r.EatRange * r.KinSearchRangeMul);
        if (victim == null || math.distance(model.Position2D, victim.Position2D) > r.EatRange * r.KinSearchRangeMul) return false;

        float own   = model.Stats.En / model.Stats.MaxEnergy;
        float their = victim.Stats.En / victim.Stats.MaxEnergy;
        if (their >= own * r.ShareFoodVictimRatio)
        {
            failure = ColonyStats.Interaction.FeedRecipientSated;
            return false;
        }

        float portion;
        if (model.Carrying > 0f) portion = model.TakeCarried();
        else portion = model.Digestion.Withdraw(model.Digestion.Capacity * r.ShareFoodTransferFraction * model.CasteMods.Feed);
        if (portion <= 0f)
        {
            failure = ColonyStats.Interaction.FeedEmpty;
            return false;
        }

        float delivered = SocialReliefMath.Delivered(portion, r.GiveTransferLoss, out float lost);
        if (lost > 0f) model.Ledger.Flows.Social += lost;

        float given = victim.Digestion.Ingest(delivered);
        _given = given;
        _needEnergy = math.max(0f, 1f - their) * victim.Stats.MaxEnergy;
        if (given > 0f)
        {
            victim.NoteGift(model.Stats);
            var stats = model.Colony?.Stats;
            stats?.RecordInteraction(ColonyStats.Interaction.Fed);
            stats?.RecordInteraction(ColonyStats.Interaction.GiftEnergy, given);
            SocialRelief.Feed(victim, model, model.EntityId, given, _needEnergy);
        }
        else model.Colony?.Stats.RecordInteraction(ColonyStats.Interaction.FeedEmpty);
        float rest  = delivered - given;
        if (rest > 0f) model.Digestion.Ingest(rest);
        if (troph && model.Nest != null)
            model.Nest.RelieveHunger(given / math.max(r.NestStorageNorm, 1e-3f) * r.TrophallaxisHungerRelief);
        model.Infection?.TryTransmitGift(model, victim);

        _holdEnd = HoldUntil(r.ShareFoodDuration);
        return true;
    }

    private EntityCommandStatus FailInteraction(ColonyStats.Interaction reason)
    {
        model.Colony?.Stats.RecordInteraction(reason);
        return Fail();
    }

    private EntityCommandStatus BeginGroom(SimulationRules r)
    {
        var patient = model.FindSickestKin(r.EatRange * r.KinSearchRangeMul, r.GroomMinLoad);
        if (patient == null) return FailInteraction(ColonyStats.Interaction.GroomNoPatient);
        return BeginGroom(r, patient);
    }

    private EntityCommandStatus BeginGroom(SimulationRules r, EntityModel patient)
    {
        model.Motor.Stop();
        patient.Virology?.Groom(r.GroomAmount);
        model.Colony?.Stats.RecordInteraction(ColonyStats.Interaction.Groomed);
        model.Infection?.TryTransmitContact(patient, model, r.GroomInfectChance);
        _holdEnd = HoldUntil(model.Tuning.IdleTime);
        return EntityCommandStatus.Running;
    }

    private bool TryBeginComfort(SimulationRules r)
    {
        if (model.Help == null) return false;
        if (model.Stats.En <= r.ComfortEnergyCost + r.StarvationThreshold) return false;

        var partner = model.FindMostStressedKin(r.EatRange * r.KinSearchRangeMul, r.ComfortMinStress, r.ComfortPairCooldown, r.ComfortTieEpsilon);
        if (partner == null) return false;

        model.Stats.En -= r.ComfortEnergyCost;
        model.Ledger.Flows.Social += r.ComfortEnergyCost;
        model.Motor.Stop();
        model.Colony?.Stats.RecordComfortStarted();
        _comfort = partner;
        _holdEnd = HoldUntil(r.ComfortDuration);
        return true;
    }

    private EntityCommandStatus FinishComfort(SimulationRules r)
    {
        var partner = _comfort;
        _comfort = null;
        if (partner == null || partner.IsDisposed || partner.IsDeathPending || partner.Colony != model.Colony
            || math.distance(model.Position2D, partner.Position2D) > r.EatRange * r.KinSearchRangeMul)
            return Fail();

        float relief = SocialRelief.Comfort(model, partner);
        model.Colony?.Stats.RecordComfortCompleted();
        return Complete(r.ComfortReward * relief);
    }
}
