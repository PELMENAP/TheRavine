public class SpeechCommand : EntityCommand
{
    private double _until;

    public SpeechCommand(EntityModel model) : base(model) { }

    protected override EntityCommandStatus OnBegin()
    {
        float cost = SimulationRules.Active.SpeechEnergyCost;
        var stats = model.Stats;
        if (decision.SpeechLen == 0 || stats.En < cost || !model.TryStartSpeechCooldown()) return Fail();

        stats.En -= cost;
        model.Ledger.Flows.Speech += cost;

        var utterance = model.MakeUtterance(decision.Tokens, decision.SpeechLen);
        model.Emit(in utterance);
        model.NoteSpeechDecision(in decision);

        _until = SimulationClock.TimeD + decision.SpeechLen * SimulationRules.Frame.SpeechSyllableSeconds;
        return EntityCommandStatus.Running;
    }

    protected override EntityCommandStatus OnTick(float dt)
        => Elapsed(_until) ? Complete() : EntityCommandStatus.Running;
}
