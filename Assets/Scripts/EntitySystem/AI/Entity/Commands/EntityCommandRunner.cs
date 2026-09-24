public enum CommandSource : byte
{
    Brain    = 0,
    Instinct = 1,
    Plan     = 2,
    Count,
}

public sealed class EntityCommandRunner
{
    private readonly EntityModel _model;
    private IEntityCommand _current;
    private BrainDecision  _decision;
    private CommandSource  _source;
    private double         _lastTick;

    private readonly int[] _startedBySource = new int[(int)CommandSource.Count];

    public EntityCommandRunner(EntityModel model) => _model = model;

    public bool IsRunning => _current != null;
    public CommandSource CurrentSource => _source;
    public int StartedBy(CommandSource source) => _startedBySource[(int)source];

    public void Start(IEntityCommand command, in BrainDecision decision, CommandSource source)
    {
        Interrupt();

        _current  = command;
        _decision = decision;
        _source   = source;
        _lastTick = SimulationClock.TimeD;
        _startedBySource[(int)source]++;

        var status = command.Begin(in decision, source);
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
        if (_source != CommandSource.Brain) return;
        _model.Brain.CompleteDecision(in _decision, command.Reward + _model.HomeostaticReturn(), SimulationClock.Time, status);
    }
}
