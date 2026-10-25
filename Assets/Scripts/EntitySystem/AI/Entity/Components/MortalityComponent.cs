using System;

public enum DeathCause : byte
{
    Killed  = 0,
    Starved = 1,
    Virus   = 2,
    Toxic   = 3,
    Age     = 4,
}

public class MortalityComponent : IComponent
{
    public event Action Died;
    private StatsComponent _stats;
    private bool _died;
    private bool _causeLocked;

    public bool IsDead => _died;
    public DeathCause Cause { get; private set; } = DeathCause.Age;

    public MortalityComponent(StatsComponent stats)
    {
        _stats = stats;
        _stats.HealthDepleted += OnHealthDepleted;
        if (_stats.Hp <= 0f) OnHealthDepleted();
    }

    private void OnHealthDepleted()
    {
        if (_died) return;
        _died = true;
        _causeLocked = true;
        Died?.Invoke();
    }

    public void NoteHarm(DeathCause cause, float healthAfter)
    {
        if (_causeLocked || healthAfter > 0f) return;
        Cause = cause;
        _causeLocked = true;
    }

    public void Dispose()
    {
        if (_stats != null) _stats.HealthDepleted -= OnHealthDepleted;
        _stats = null;
        Died = null;
    }
}