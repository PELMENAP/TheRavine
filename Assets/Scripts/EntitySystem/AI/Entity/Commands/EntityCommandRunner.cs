public sealed class EntityCommandRunner
{
    private readonly EntityModel _model;
    private IEntityCommand _current;
    private BrainDecision  _decision;
    private double         _lastTick;

    public EntityCommandRunner(EntityModel model) => _model = model;

    public bool IsRunning => _current != null;

    public void Start(IEntityCommand command, in BrainDecision decision)
    {
        Interrupt();

        _current  = command;
        _decision = decision;
        _lastTick = SimulationClock.TimeD;

        var status = command.Begin(in decision);
        if (status != EntityCommandStatus.Running && ReferenceEquals(_current, command))
            Finish(status);
    }

    public void Tick()
    {
        var command = _current;
        if (command == null) return;

        double now = SimulationClock.TimeD;
        float  dt  = (float)(now - _lastTick);
        _lastTick = now;

        var status = command.Tick(dt);
        if (status != EntityCommandStatus.Running && ReferenceEquals(_current, command))
            Finish(status);
    }

    public void Interrupt()
    {
        var command = _current;
        if (command == null) return;

        command.Cancel();
        if (ReferenceEquals(_current, command))
            Finish(EntityCommandStatus.Interrupted);
    }

    public void Abandon()
    {
        var command = _current;
        _current = null;
        command?.Cancel();
    }

    private void Finish(EntityCommandStatus status)
    {
        var command = _current;
        _current = null;
        _model.Brain.CompleteDecision(in _decision, command.Reward, SimulationClock.Time, status);
    }
}