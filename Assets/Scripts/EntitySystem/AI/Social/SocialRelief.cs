using Unity.Mathematics;

public static class SocialReliefMath
{
    public static float ComfortRelief(float partnerStress, float accum, float relief, float cap)
    {
        if (!(cap > 0f) || !(partnerStress > 0f)) return 0f;
        float room = math.max(0f, cap - accum);
        float amount = relief * partnerStress * math.max(0f, 1f - accum / cap);
        return math.min(math.min(amount, room), partnerStress);
    }

    public static float FeedRelief(float given, float needEnergy, float receiverStress, float feedRelief, bool reciprocal)
    {
        if (reciprocal || !(given > 0f) || !(needEnergy > 0f)) return 0f;
        return feedRelief * math.saturate(given / needEnergy) * receiverStress;
    }

    public static float Delivered(float portion, float loss, out float lost)
    {
        lost = portion * loss;
        return portion - lost;
    }

    public static float ReturnToGiver(DigestionComponent stomach, float rest)
    {
        if (!(rest > 0f)) return 0f;
        return rest - stomach.Ingest(rest);
    }
}

public static class SocialRelief
{
    public static bool IsReciprocal(EntityModel receiver, EntityModel giver, int giverId, float now, float window)
    {
        var gl = giver != null ? giver.Help : null;
        if (gl != null && giver.ManagerIndex >= 0 && gl.ReceivedWithin(giver.ManagerIndex, receiver.EntityId, now, window)) return true;
        var rl = receiver.Help;
        return rl != null && receiver.ManagerIndex >= 0 && rl.GaveWithin(receiver.ManagerIndex, giverId, now, window);
    }

    public static float Feed(EntityModel receiver, EntityModel giver, int giverId, float given, float needEnergy)
    {
        var r = SimulationRules.Active;
        float now = SimulationClock.Time;
        int receiverId = receiver.EntityId;
        var rl = receiver.ManagerIndex >= 0 ? receiver.Help : null;
        var gl = giver != null && giver.ManagerIndex >= 0 ? giver.Help : null;

        bool reciprocal = IsReciprocal(receiver, giver, giverId, now, r.HelpReciprocalWindow);
        var stats = giver != null ? giver.Colony?.Stats : receiver.Colony?.Stats;
        if (stats != null)
        {
            stats.GiveTransfers++;
            if (reciprocal) stats.GiveReciprocal++;
        }

        rl?.NoteReceived(receiver.ManagerIndex, giverId, given, now, r.HelpBalanceTau);
        gl?.NoteGiven(giver.ManagerIndex, receiverId, given, now, r.HelpBalanceTau);
        AnomalyDetector.Current?.NoteTransfer(giverId, receiverId, given, SimulationClock.TimeD);

        if (!SimulationRules.Frame.SocialReliefEnabled || (rl == null && gl == null)) return 0f;

        float relief = SocialReliefMath.FeedRelief(given, needEnergy, receiver.Stress, r.FeedRelief, reciprocal);
        float actual = receiver.RelieveStress(relief, ColonyStats.ReliefKind.Feed);
        if (giver != null) giver.RelieveStress(r.GiveSelfShare * actual, ColonyStats.ReliefKind.Feed);
        if (actual > 0f) AnomalyDetector.Current?.NoteRelief(giverId, receiverId, actual);
        return actual;
    }

    public static float Comfort(EntityModel giver, EntityModel partner)
    {
        var r = SimulationRules.Active;
        var gl = giver.Help;
        var pl = partner.Help;
        if (gl == null || giver.ManagerIndex < 0 || partner.ManagerIndex < 0) return 0f;

        float now = SimulationClock.Time;
        int giverId = giver.EntityId, partnerId = partner.EntityId;
        if (!gl.ComfortReady(giver.ManagerIndex, partnerId, now, r.ComfortPairCooldown)) return 0f;

        float accum = gl.ComfortAccum(giver.ManagerIndex, partnerId, now, r.ComfortPairWindow);
        float relief = SocialReliefMath.ComfortRelief(partner.Stress, accum, r.ComfortRelief, r.ComfortPairCap);
        if (partner.Stress < r.ComfortMinStress) relief = 0f;
        float actual = partner.RelieveStress(relief, ColonyStats.ReliefKind.Comfort);
        giver.RelieveStress(r.ComfortSelfShare * actual, ColonyStats.ReliefKind.Comfort);

        gl.NoteComfort(giver.ManagerIndex, partnerId, actual, now, r.ComfortPairWindow);
        pl?.NoteComfort(partner.ManagerIndex, giverId, actual, now, r.ComfortPairWindow);
        if (actual > 0f) AnomalyDetector.Current?.NoteRelief(giverId, partnerId, actual);
        return actual;
    }
}
